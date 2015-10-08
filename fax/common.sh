# Copyright (C) 2007-2015, Manuel Meitinger
# This program is free software, distributed under the terms of
# the GNU General Public License Version 2. See the LICENSE file
# at the top of the source tree.

SERVERMAIL="no-reply@aufbauwerk.com"
FROMSTRING="PBX"

################################################################################

ROOT=/var/spool/asterisk/fax/${MESSAGE_ID}
LOG=/var/log/asterisk/fax_log

do () {
	echo -n "$(date +'%F %T') ${MESSAGE_ID}: $1: " >> ${LOG}
	(eval $2) > /dev/null 2>> ${LOG}
	ERROR=$?
	echo "[${ERROR}]" >> ${LOG}
	if [ ${ERROR} -ne 0 ]; then
		echo "An internal error occured. ($1)" 2>&1
		exit ${ERROR}
	fi
}

die () {
    echo "$1" 2>&1
    exit 1
}

[ "${MESSAGE_ID}" -ne "" ] || die "ERROR: MESSAGE_ID must be set"
