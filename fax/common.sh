# Copyright (C) 2007-2015, Manuel Meitinger
# This program is free software, distributed under the terms of
# the GNU General Public License Version 2. See the LICENSE file
# at the top of the source tree.

SERVERMAIL="no-reply@aufbauwerk.com"
FROMSTRING="PBX"

################################################################################

ROOT=/var/spool/asterisk/fax/${MESSAGE_ID}
LOG=/var/log/asterisk/fax_log
TEMP=$(mktemp -d) || exit $?

die () {
	echo "$1" 2>&1
	rm -rf "${TEMP}" 2> /dev/null
	exit 1
}

finish () {
	rm -rf "${TEMP}" 2> /dev/null
	exit 0
}

run () {
	echo -n "$(date +'%F %T') ${MESSAGE_ID}: $1: " >> ${LOG}
	eval $2 > /dev/null 2>> ${LOG}
	[ $? -eq 0 ] || die "An internal error occured. ($1)"
	echo "OK" >> ${LOG}
}

send () {
	run "store mail text"  'echo "${MESSAGE}" > "${TEMP}/send.txt"'
	run "create mime file" 'mpack -s "${SUBJECT}" -d "${TEMP}/send.txt" -c image/tiff -o "${TEMP}/send.mime" "'"$1"'"'
	run "send mail to $2"  'sendmail -i -f "${SERVERMAIL}" -F "${FROMSTRING}" "'"$2"'" < "${TEMP}/send.mime"'
}

[ -n "${MESSAGE_ID}" ] || die "ERROR: MESSAGE_ID must be set"
