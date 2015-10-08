#!/bin/sh
# Copyright (C) 2007-2015, Manuel Meitinger
# This program is free software, distributed under the terms of
# the GNU General Public License Version 2. See the LICENSE file
# at the top of the source tree.

SUBJECT="[fax] Neues Fax von ${SENDER}"
MESSAGE=$(cat << EOF
Dies ist eine automatisch generierte Nachricht.

Sie haben ein ${PAGES}-seitiges Fax von der Gegenstelle ${STATIONID} erhalten.

Das Fax befindet sich im Anhang.

Mit freundlichen Grüßen,
Ihre Telefonanlage
EOF
)

################################################################################

. /var/spool/asterisk/fax/common.sh

[ $# -eq 1 ] || die "USAGE: $0 recipient"

do "write success message" 'echo "${MESSAGE}" > "${ROOT}.txt"'
do "convert sff to tif"    'sfftobmp -tif "${ROOT}.sff" -o "${ROOT}.tif"'
do "create mime file"      'mpack -s "${SUBJECT}" -d "${ROOT}.txt" -c image/tiff -o "${ROOT}.mime" "${ROOT}.tif"'
do "send mail to $1"       'sendmail -i -f "${SERVERMAIL}" -F "${FROMSTRING}" "$1" < "${ROOT}/mime"'
do "cleanup files"         'rm -f "${ROOT}.sff" "${ROOT}.txt" "${ROOT}.tif" "${ROOT}.mime"'

exit 0
