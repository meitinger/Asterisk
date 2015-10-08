#!/bin/sh
# Copyright (C) 2007-2015, Manuel Meitinger
# This program is free software, distributed under the terms of
# the GNU General Public License Version 2. See the LICENSE file
# at the top of the source tree.

CHANNEL="CAPI/g0/53:${LOCAL_PART}/Bo"
CALLERID='"AufBauWerk" <+4351258581453>'
TIMEOUT=600
WAITTIME=30
MAXRETRIES=2
RETRYTIME=180

SUCCESS_SUBJECT="[fax] Sendebestätigung"
SUCCESS_MESSAGE=$(cat << EOF
Dies ist eine automatisch generierte Nachricht.

Ihr Fax an ${LOCAL_PART} wurde erfolgreich an Gegenstelle ${STATIONID} übermittelt.

Eine Kopie des Faxes befindet sich im Anhang.

Mit freundlichen Grüßen,
Ihre Telefonanlage
EOF
)

FAILURE_SUBJECT="[fax] Fehlerbericht"
FAILURE_HEADER=$(cat << EOF
Dies ist eine automatisch generierte Nachricht.

Beim Sendeversuch an Faxnummer ${LOCAL_PART} trat folgender Fehler auf:

> 
EOF
)
FAILURE_MESSAGE_TIMEOUT="Die maximale Sendedauer von ${TIMEOUT} Sekunden wurde überschritten."
FAILURE_MESSAGE_TRANSMISSION="Bei der Übermittlung trat ein Fehler auf."
FAILURE_MESSAGE_BUSY="Bei der gewählten Nummer ist derzeit besetzt."
FAILURE_MESSAGE_HANGUP="Die Gegenstelle hat aufgelegt."
FAILURE_MESSAGE_NOANSWER="Die Gegenstelle hat nach ${WAITTIME} Sekunden Klingeln nicht geantwortet."
FAILURE_MESSAGE_CONGESTION="Die ausgehende Leitung ist belegt."
FAILURE_MESSAGE_UNKNOWN="Ein unbekannter Fehler ist aufgetreten. ($1)"
FAILURE_FOOTER_RETRY=$(cat << EOF


In ${RETRYTIME} Sekunden wird ein weiterer Zustellversuch gestartet.

Mit freundlichen Grüßen,
Ihre Telefonanlage
EOF
)
FAILURE_FOOTER_LAST=$(cat << EOF


Dies war der letzte Zustellversuch.

Bitte kontrollieren Sie die Nummer und senden Sie das Fax später erneut. Eine Kopie befindet sich im Anhang.

Mit freundlichen Grüßen,
Ihre Telefonanlage
EOF
)

##############################################################################

. /var/spool/asterisk/fax/common.sh

[ $# -lt 2 ] || die "USAGE: $0 [result]"

CALLFILE=$(cat << EOF
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
)

if [ $# -eq 0 ]; then
	run "save mail to file"   'cat - > "${TEMP}/mime"'
	run "create unpack dir"   'mkdir "${TEMP}/unpack/"'
	run "extract attachments" 'munpack -f -q -C "${TEMP}/unpack/" "${TEMP}/mime"'
	run "find tif files"      'TIFS=$(find "${TEMP}/unpack/" -wholename "*.tif?" -type f)'
	[ -n "${TIFS}" ] || die "The e-mail doesn't contain any tifs."
	run "count tif files"     'TIFS_COUNT=$(echo "${TIFS}" | wc -l)'
	[ ${TIFS_COUNT} -eq 1 ] || die "The e-mail contains more than one tif."
	run "copy tif file"       'cp -f -T "${TIFS}" "${ROOT}.tif"'
	run "create ps file"      'tiff2ps -h 11.69 -w 8.27 -O "${TEMP}/ps" "${ROOT}.tif"'
	run "create sff file"     'gs -q -dNOPAUSE -dBATCH -dSAFER -sDEVICE=cfax   -sOutputFile="${ROOT}.sff" "${TEMP}/ps"'
	run "store sender"        'echo "${SENDER}" > "${ROOT}.sender"'
	run "create call file"    'echo "${CALLFILE}" > "${TEMP}/call"'
	run "move call file"      'mv -f -T "${TEMP}/call" "/var/spool/asterisk/outgoing/${MESSAGE_ID}.call"'
else
	run "restore sender" 'SENDER=$(cat "${ROOT}.sender")'
	run "count retries"  'RETRIES=$(grep -c '\''^RETRY$'\'' "/var/spool/asterisk/outgoing/${MESSAGE_ID}.call")'

	# check if this is the last try
	if [ ${RETRIES} -lt ${MAXRETRIES} ]; then
		FAILURE_FOOTER=${FAILURE_FOOTER_RETRY}
		CLEANUP=0
	else
		FAILURE_FOOTER=${FAILER_FOOTER_LAST}
		CLEANUP=1
	fi

	# handle the different results
	case "$1" in
		-s)
			SUBJECT=${SUCCESS_SUBJECT}
			MESSAGE=${SUCCESS_MESSAGE}
			CLEANUP=1
			;;
		-x)
			SUBJECT=${FAILURE_SUBJECT}
			MESSAGE=${FAILURE_HEADER}${FAILURE_MESSAGE_TRANSMISSION}${FAILURE_FOOTER_LAST}
			CLEANUP=1
			;;
		-t)
			SUBJECT=${FAILURE_SUBJECT}
			MESSAGE=${FAILURE_HEADER}${FAILURE_MESSAGE_TIMEOUT}${FAILURE_FOOTER_LAST}
			CLEANUP=1
			;;
		-1)
			SUBJECT=${FAILURE_SUBJECT}
			MESSAGE=${FAILURE_HEADER}${FAILURE_MESSAGE_HANGUP}${FAILURE_FOOTER}
			;;
		-3)
			SUBJECT=${FAILURE_SUBJECT}
			MESSAGE=${FAILURE_HEADER}${FAILURE_MESSAGE_NOANSWER}${FAILURE_FOOTER}
			;;
		-5)
			SUBJECT=${FAILURE_SUBJECT}
			MESSAGE=${FAILURE_HEADER}${FAILURE_MESSAGE_BUSY}${FAILURE_FOOTER}
			;;
		-8)
			SUBJECT=${FAILURE_SUBJECT}
			MESSAGE=${FAILURE_HEADER}${FAILURE_MESSAGE_CONGESTION}${FAILURE_FOOTER}
			;;
		*)
			SUBJECT=${FAILURE_SUBJECT}
			MESSAGE=${FAILURE_HEADER}${FAILURE_MESSAGE_UNKNOWN}${FAILURE_FOOTER}
			;;
	esac

	send "${ROOT}.tif" "${SENDER}"
	[ ${CLEANUP} -ne 0 ] && run "cleanup spool files" 'rm -f "${ROOT}.tif" "${ROOT}.sff" "${ROOT}.sender"'
fi

finish
