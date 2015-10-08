#!/bin/sh
# Copyright (C) 2007-2015, Manuel Meitinger
# This program is free software, distributed under the terms of
# the GNU General Public License Version 2. See the LICENSE file
# at the top of the source tree.

CHANNEL="CAPI/g0/33:${LOCAL_PART}/Bo"
CALLERID='"Bad Haering" <+4353329330033>'
TIMEOUT=600
WAITIME=30
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
EOF
)
FAILURE_MESSAGE_TIMEOUT="Die maximale Sendedauer von ${TIMEOUT} Sekunden wurde überschritten."
FAILURE_MESSAGE_TRANSMISSION="Bei der Übermittlung trat ein Fehler auf."
FAILURE_MESSAGE_BUSY="Bei der gewählten Nummer ist zur Zeit besetzt."
FAULURE_MESSAGE_HANGUP="Die Gegenstelle hat aufgelegt."
FAILURE_MESSAGE_NOANSWER="Die Gegenstelle hat nach ${WAITIME} Sekunden Klingeln nicht geantwortet."
FAILURE_MESSAGE_CONGESTION="Die ausgehende Leitung ist belegt."
FAILURE_MESSAGE_UNKNOWN="Ein unbekannter Fehler ist aufgetreten. ($1)"
FAILURE_FOOTER=$(cat << EOF

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
	do "save mail to file"   'cat - > "${ROOT}.mime"'
	do "create unpack dir"   'UNPACK=$(mktmp -d)'
	do "extract attachments" 'munpack -f -q -C "${UNPACK}" "${ROOT}.mime"'
	do "find tif files"      'TIFS=$(find "${UNPACK}" -wholename "*.tif" -type f)'
	do "count tif files"     'TIFS_COUNT=$(echo "$TIFS" | wc -l)'
	[ ${TIFS_COUNT} -eq 1 ] && do "copy tif file" 'cp -f -T "${TIFS}" "${ROOT}.tif"'
	rm -r -f "${UNPACK}" 
	[ ${TIFS_COUNT} -eq 0 ] && die "The e-mail doesn't contain any tifs."
	[ ${TIFS_COUNT} -gt 1 ] && die "The e-mail contains more than one tif."
	do "create ps file"      'gs -q -dNOPAUSE -dBATCH -dSAFER -sDEVICE=psgray -sOutputFile=- "${ROOT}.tif" | perl -n -e '\''if(/^([0-9]+) ([0-9]+) (null|\/(a[0-4]|letter|legal|tabloid|ledger|archE)) setpagesize$/){print(($1>$2?"842 595":"595 842")." null setpagesize\n<</Orientation ".($1>$2?3:0).">> setpagedevice\n");}else{print();}'\'' > "${ROOT}.ps"' 
	do "create sff file"     'gs -q -dNOPAUSE -dBATCH -dSAFER -sDEVICE=cfax   -sOutputFile="${ROOT}.sff" "${ROOT}.ps"' "create sff file"
	do "store sender"        'echo "${SENDER}" > "{ROOT}.sender"'
	do "create call file"    'echo "${CALLFILE} > "${ROOT}.call"'
	do "move call file"      'mv -f -T "${ROOT}.call" "/var/spool/asterisk/outgoing/${MESSAGE_ID}.call"'
elif
	do "count retries" 'RETRIES=$(grep '\''^RETRY$'\'' "/var/spool/asterisk/outgoing/${MESSAGE_ID}.call" | wc -l)'
	
	case "$1" in
		-s)
			SUBJECT=${SUCCESS_SUBJECT}
			MESSAGE=${SUCCESS_MESSAGE}
			;;
		-x)
			SUBJECT="${FAILURE}"
			;;
		-t)
			SUBJECT="${SUBJECT_FAILURE}"
			;;
		-1)
			;;
		-3)
			;;
		-5)
			;;
		-8)
			;;
fi
exit 0
