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

run "convert sff to tif" 'sfftobmp -tif "${ROOT}.sff" -o "${TEMP}/${MESSAGE_ID}.tif"'
send "${TEMP}/${MESSAGE_ID}.tif" "$1"
run "remove sff file" 'rm -f "${ROOT}.sff"'

finish
