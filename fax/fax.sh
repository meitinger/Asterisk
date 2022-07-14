#!/bin/sh
# Copyright (C) 2007-2020, Manuel Meitinger
# This program is free software, distributed under the terms of
# the GNU General Public License Version 2. See the LICENSE file
# at the top of the source tree.

case "${LOCAL_PART}" in
	+43*) DIAL_NUMBER="0${LOCAL_PART##+43}" ;;
	+*) DIAL_NUMBER="00${LOCAL_PART##+}" ;;
	*) DIAL_NUMBER="${LOCAL_PART}" ;;
esac

SERVERMAIL='no-reply@aufbauwerk.com'
FROMSTRING='PBX'
CHANNEL="CAPI/g0/53:${DIAL_NUMBER}/Bo"
CALLERID='"AufBauWerk" <+4351258581453>'
TIMEOUT=600
WAITTIME=30
MAXRETRIES=2
RETRYTIME=180

################################################################################

RECEIVED_SUBJECT="[fax] Neues Fax von ${SENDER}"
RECEIVED_MESSAGE="$(cat << EOF
Dies ist eine automatisch generierte Nachricht.

Sie haben ein ${PAGES}-seitiges Fax von der Gegenstelle ${STATIONID} erhalten.

Das Fax befindet sich im Anhang.

Mit freundlichen Grüßen,
Ihre Telefonanlage
EOF
)"

SENT_SUBJECT='[fax] Sendebestätigung'
SENT_MESSAGE="$(cat << EOF
Dies ist eine automatisch generierte Nachricht.

Ihr Fax an ${LOCAL_PART} wurde erfolgreich an Gegenstelle ${STATIONID} übermittelt.

Eine Kopie des Faxes befindet sich im Anhang.

Mit freundlichen Grüßen,
Ihre Telefonanlage
EOF
)"

FAILURE_SUBJECT='[fax] Fehlerbericht'
FAILURE_HEADER="$(cat << EOF
Dies ist eine automatisch generierte Nachricht.

Beim Sendeversuch an Faxnummer ${LOCAL_PART} trat folgender Fehler auf:

> 
EOF
)"
FAILURE_MESSAGE_TIMEOUT="Die maximale Sendedauer von ${TIMEOUT} Sekunden wurde überschritten."
FAILURE_MESSAGE_TRANSMISSION='Bei der Übermittlung trat ein Fehler auf.'
FAILURE_MESSAGE_BUSY='Bei der gewählten Nummer ist derzeit besetzt.'
FAILURE_MESSAGE_HANGUP='Die Gegenstelle hat aufgelegt.'
FAILURE_MESSAGE_NOANSWER="Die Gegenstelle hat nach ${WAITTIME} Sekunden Klingeln nicht geantwortet."
FAILURE_MESSAGE_CONGESTION='Die ausgehende Leitung ist belegt.'
FAILURE_MESSAGE_UNKNOWN='Ein unbekannter Fehler ist aufgetreten.'
FAILURE_FOOTER_RETRY="$(cat << EOF


In ${RETRYTIME} Sekunden wird ein weiterer Zustellversuch gestartet.

Mit freundlichen Grüßen,
Ihre Telefonanlage
EOF
)"
FAILURE_FOOTER_LAST="$(cat << EOF


Dies war der letzte Zustellversuch.

Bitte kontrollieren Sie die Nummer und senden Sie das Fax später erneut. Eine Kopie befindet sich im Anhang.

Mit freundlichen Grüßen,
Ihre Telefonanlage
EOF
)"

################################################################################

ROOT="/var/spool/asterisk/fax/${MESSAGE_ID}"
LOG='/var/log/asterisk/fax_log'
TEMP="$(mktemp -d)" || exit $?
CALLFILE="$(cat << EOF
Channel: ${CHANNEL}
CallerID: ${CALLERID}
Context: fax-outgoing
Extension: send
Priority: 1
MaxRetries: ${MAXRETRIES}
RetryTime: ${RETRYTIME}
WaitTime: ${WAITTIME}
SetVar: TIMEOUT=${TIMEOUT}
SetVar: MESSAGE_ID=${MESSAGE_ID}
SetVar: LOCAL_PART=${LOCAL_PART}
EOF
)"


die () {
	echo "$1" 2>&1
	rm -rf "${TEMP}" 2> /dev/null
	exit 1
}

run () {
	echo -n "$(date +'%F %T') ${MESSAGE_ID}: $1: " >> ${LOG}
	eval $2 > /dev/null 2>> ${LOG}
	[ $? -eq 0 ] || die "An internal error occured. ($1)"
	echo 'OK' >> ${LOG}
}

send () {
	run 'store mail text'  'echo "${MESSAGE}" > "${TEMP}/send.txt"'
	run 'create mime file' 'mpack -s "${SUBJECT}" -d "${TEMP}/send.txt" -c image/tiff -o "${TEMP}/send.mime" "$3"' "$1"
	run "send mail to $2"  'sendmail -i -f "${SERVERMAIL}" -F "${FROMSTRING}" "$3" < "${TEMP}/send.mime"'          "$2"
}

################################################################################

[ -n "${MESSAGE_ID}" ] || die 'ERROR: MESSAGE_ID must be set'
[ $# -ge 1 ] || die "USAGE: $0 send | receive <address> | confirm -<code>"

case "$1" in
	send)
		[ $# -eq 1 ] || die "USAGE: $0 send"

		run 'save mail to file'   'cat - > "${TEMP}/mime"'
		run 'create unpack dir'   'mkdir "${TEMP}/unpack/"'
		run 'extract attachments' 'munpack -f -q -C "${TEMP}/unpack/" "${TEMP}/mime"'
		run 'find tif files'      'TIFS="$(find "${TEMP}/unpack/" -wholename '\''*.tif'\'' -type f)"'
		[ -n "${TIFS}" ] || die 'The e-mail does not contain any tifs.'
		run 'count tif files'     'TIFS_COUNT=$(echo "${TIFS}" | wc -l)'
		[ ${TIFS_COUNT} -eq 1 ] || die 'The e-mail contains more than one tif.'
		run 'copy tif file'       'cp -f -T "${TIFS}" "${ROOT}.tif"'
		run 'create ps file'      'tiff2ps -h 11.69 -w 8.27 -O "${TEMP}/ps" "${ROOT}.tif"'
		run 'create sff file'     'gs -q -dNOPAUSE -dBATCH -dSAFER -sDEVICE=cfax -sOutputFile="${ROOT}.sff" "${TEMP}/ps"'
		run 'store sender'        'echo "${SENDER}" > "${ROOT}.sender"'
		run 'create call file'    'echo "${CALLFILE}" > "${TEMP}/call"'
		run 'move call file'      'mv -f -T "${TEMP}/call" "/var/spool/asterisk/outgoing/${MESSAGE_ID}.call"'
		;;

	receive)
		[ $# -eq 2 ] || die "USAGE: $0 receive <address>"

		SUBJECT="${RECEIVED_SUBJECT}"
		MESSAGE="${RECEIVED_MESSAGE}"

		run 'convert sff to tif' 'sfftobmp -tif "${ROOT}.sff" -o "${TEMP}/${MESSAGE_ID}.tif"'
		send "${TEMP}/${MESSAGE_ID}.tif" "$2"
		run 'remove sff file' 'rm -f "${ROOT}.sff"'
		;;

	confirm)
		[ $# -eq 2 ] || die "USAGE: $0 confirm -<code>"

		run 'restore sender' 'SENDER="$(cat "${ROOT}.sender")"'
		run 'count retries'  'RETRIES=$(grep -c "^StartRetry:" "/var/spool/asterisk/outgoing/${MESSAGE_ID}.call")'

		if [ ${RETRIES} -le ${MAXRETRIES} ]; then
			FAILURE_FOOTER="${FAILURE_FOOTER_RETRY}"
			CLEANUP=0
		else
			FAILURE_FOOTER="${FAILURE_FOOTER_LAST}"
			CLEANUP=1
		fi

		case "$2" in
			-s)
				SUBJECT="${SENT_SUBJECT}"
				MESSAGE="${SENT_MESSAGE}"
				CLEANUP=1
				;;
			-x)
				SUBJECT="${FAILURE_SUBJECT}"
				MESSAGE="${FAILURE_HEADER}${FAILURE_MESSAGE_TRANSMISSION}${FAILURE_FOOTER_LAST}"
				CLEANUP=1
				;;
			-t)
				SUBJECT="${FAILURE_SUBJECT}"
				MESSAGE="${FAILURE_HEADER}${FAILURE_MESSAGE_TIMEOUT}${FAILURE_FOOTER_LAST}"
				CLEANUP=1
				;;
			-1)
				SUBJECT="${FAILURE_SUBJECT}"
				MESSAGE="${FAILURE_HEADER}${FAILURE_MESSAGE_HANGUP}${FAILURE_FOOTER}"
				;;
			-3)
				SUBJECT="${FAILURE_SUBJECT}"
				MESSAGE="${FAILURE_HEADER}${FAILURE_MESSAGE_NOANSWER}${FAILURE_FOOTER}"
				;;
			-5)
				SUBJECT="${FAILURE_SUBJECT}"
				MESSAGE="${FAILURE_HEADER}${FAILURE_MESSAGE_BUSY}${FAILURE_FOOTER}"
				;;
			-8)
				SUBJECT="${FAILURE_SUBJECT}"
				MESSAGE="${FAILURE_HEADER}${FAILURE_MESSAGE_CONGESTION}${FAILURE_FOOTER}"
				;;
			*)
				SUBJECT="${FAILURE_SUBJECT}"
				MESSAGE="${FAILURE_HEADER}${FAILURE_MESSAGE_UNKNOWN}${FAILURE_FOOTER}"
				;;
		esac

		send "${ROOT}.tif" "${SENDER}"
		[ ${CLEANUP} -ne 0 ] && run 'cleanup spool files' 'rm -f "${ROOT}.tif" "${ROOT}.sff" "${ROOT}.sender"'
		;;

	esac

rm -rf "${TEMP}" 2> /dev/null
exit 0
