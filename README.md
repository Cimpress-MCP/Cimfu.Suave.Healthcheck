# Cimfu.Suave.Healthcheck

A pluggable healthcheck endpoint for the [Suave][suave] functional web server. Based on the
[healthcheck.spec specification][hcspec], with a modification that allows a message to be
recorded on health checks that pass.

Documentation for this library can be found on [GitHub Pages][gh].

## Build status

This project is automatically built by [Travis CI][travisci] and [AppVeyor][appveyor]. The current status
of the master branch:

 * AppVeyor (Windows): [![Build status](https://ci.appveyor.com/api/projects/status/d74rs3wagh1mxti1/branch/master?svg=true)][appveyor]

 * Travis CI (Mono on Linux): [![Build Status](https://travis-ci.org/Cimpress-MCP/Cimfu.Suave.Healthcheck.svg?branch=master)][travisci]

  [gh]: https://cimpress-mcp.github.io/Cimfu.Suave.Healthcheck
  [suave]: http://suave.io
  [hcspec]: https://github.com/Cimpress-MCP/healthcheck.spec
  [travisci]: https://travis-ci.org/Cimpress-MCP/Cimfu.Suave.Healthcheck
  [appveyor]: https://ci.appveyor.com/project/neoeinstein/cimfu-suave-healthcheck/branch/master
