# GSPro4 OSP

GSPro4OSP is a plugin for [OpenSkyPlus](https://github.com/OpenSkyPlus/OpenSkyPlus) which will connect your launch monitor to GSPro's Open API.

Currently only one launch monitor is supported.

<br>
<br>
<br>

## How to Use

**Make sure you have GSPro and selected the Open API license. This is mandatory.**

0) Ensure OpenSkyPlus is installed for your launch monitor.
1) Head to the [releases](https://github.com/OpenSkyPlus/GSPro4OSP/releases) page and grab the latest release.
2) Extract the release zip to `C:\Program Files\[Your Launch Monitor Software]\BepInEx\plugins\OpenSkyPlus\plugins\GSPro4OSP`
3) Open GSPro, when prompted, choose to activate the Open API
4) Open your launch monitor software and wait for the green light in OpenSkyPlus

<br>
<br>
<br>

## Contributing

Pull requests are welcome from the community.

<br>
<br>
<br>

## Config Settings

This plugin has various config settings. They can be found in `C:\Program Files\[Your Launch Monitor Software]\BepInEx\plugins\OpenSkyPlus\plugins\GSPro4OSP\pluginsettings.json`

Unless you have a good reason, do not change Hostname or Port. 
<br>
<br>
**MaxRetries** can be set to a positive number to prevent OSP from looking for GSPro forever.
<br>
<br>
**RetryDelay** can be modified to change the time, in ms, between connection attempts to GSPro's Open API.
<br>
<br>
**DistanceToPtMode** is the distance the ball must be from the pin in order to activate putting mode on your launch monitor (selecting the putter in game will always activate putting mode).
<br>
This can also be set to 0, to only have putting mode be based on the club selected (LW, PW, and PT) or -1, which means never use putting mode (except when selecting the putter).

<br>
<br>
<br>

## Troubleshooting

More often than not, if GSPro stops reading shots from OSP, the solution is to close the Open API, then reinitialize it from GSPro's settings. OSP will automatically reconnect.

If the monitor is not arming, sometimes switching the club between PT and another club will resolve it. If not, use the debugging buttons available in OSP.

If you're still having issues, in your launch monitor software, click the black OSP box in the bottom-left corner of the screen and check the log for any errors.