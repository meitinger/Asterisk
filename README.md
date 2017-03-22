AufBauWerk Asterisk
===================


Description
-----------
This repository should be considered *internal*. It contains the public config
and install files of AufBauWerk's Asterisk servers, as well as a program that
syncs the device states (for BLF) across servers.


Files
-----
* `install.txt`: shell commands to setup a server
* `commands.txt`: list of available dial/DTFM options
* `audio.tar.gz`: German audio files


Folders
-------
* `fax`: shell scripts and dialplan extensions to send and receive faxes
* `isdn`: files necessary to setup an AVM B1 card with Asterisk 1.8
* `config`: all public configuration files including the dialplan
* `BLF`: a C# program that syncs the device state across multiple PBXs


Where to start
--------------
Once you've had a look at `commands.txt` or `config/extensions.conf` and
decided that this setup might suit your organization, please consider the
following:

1. You have to at least set `BRANCHES` and `BRANCHES_LIST` to your needs.
   Each branch office will be assigned a number. If you need more than one
   digit, you have to adjust `BRANCHES_LEN` and maybe others like `PADDING`
   as well.
2. You have to provide at least `extensions_local.conf`, `manager_local.conf`,
   `modules_local.conf` and `users_local.conf` by our own and place them into
   the `config` directory.
3. `extensions_local.conf`:
   1. The variable `BRANCH` must be defined in a `[globals](+)` section.
   2. The sections `[from-external-local]`, `[from-transfer-local]` and
      `[from-internal-local]` must be present (but may be empty).
   3. You should consider defining some programs in a `[program](+)` section.
      Programs start at priority `1` and should end in `Return(<vmoptions>)` in
      order to launch the branch mailbox afterwards, where `<vmoptions>` is
      usually either `b` or `u`, but can be any `VoiceMail` option.
   4. The extension `string` must be defined in context `[dial](+)` and return
      the full `Dial` string for outgoing calls. It's first argument is the
      called number, the second (optional) argument the calling extension.
4. `users_local.conf`:
   1. All phones are defined as a mixture of permission group and device type,
      e.g. `[11](management,gxp2000)`.
   2. Using `setvar=PRIVILEGE_xy=1` you can enable action `xy`, setting the
      value to `0` will disable it. (See `extensions.conf` for a list of defined
      permissions, you can of course extend it.)
   3. The `callerid` must be set in full, like `"John Doe" <+1 555 5555-11>`
   4. All other branches must be listed as `[<id>](trunk)` where `<id>` is the
      value defined in `BRANCH` and `BRANCHES`. The `callerid` must also be set
      in full and without any trailing extension.
5. For more information, feel free to contact [me](mailto:m.meitinger@aufbauwerk.com).
