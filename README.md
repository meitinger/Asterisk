AufBauWerk Asterisk
===================

Description
-----------
This repository should be considered *internal*.
It contains the public configuration and installation files of AufBauWerk's
Asterisk servers, as well as a program that syncs the device states (for BLF)
across servers and sends texts for missed calls using Magenta's SMS.

Folders
-------
* `config`: all public configuration files including the dialplan
* `fax`: shell scripts and dialplan extensions to send and receive faxes
* `isdn`: files necessary to setup an AVM B1 card with Asterisk 1.8
* `manager`: a C# program that syncs the device state across multiple PBXs
