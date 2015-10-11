(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Cimfu.Suave.Healthcheck"

(**
Cimfu.Suave.Healthcheck
======================

A pluggable healthcheck endpoint for the [Suave][suave] functional web server. Based on the
[healthcheck.spec specification][hcspec], with a modification that allows a message to be
recorded on health checks that pass.

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The Cimfu.Suave.Healthcheck library can be <a href="https://nuget.org/packages/Cimfu.Suave.Healthcheck">installed from NuGet</a>:
      <pre>PM> Install-Package Cimfu.Suave.Healthcheck</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

Example
-------

This example demonstrates attaching the main server switch healthcheck to a Suave `WebPart`.

*)
#r "Suave.dll"
#r "Cimfu.Suave.Healthcheck.dll"
open Suave.Http
open Cimfu.Suave.Healthcheck

let myApp = RequestErrors.NOT_FOUND "Not here"

let healthchecks = Map.ofList ["main", Checks.serverMainSwitch]

let app =
  myApp
  |> withHealthcheck healthchecks

(**

Samples & documentation
-----------------------

For additional documentation, see our tutorial and API reference. The [source][gh] is also well-documented.

 * [Tutorial](tutorial.html) contains a few examples of how you can use the healthcheck features provided
   by this library.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.

Versioning
----------

This project is versioned following [SemVer v2.0][semver2] conventions. The library is currently in a
pre-version-1.0 state, which means that minor version increments may contain breaking changes. Once version
1.0 is released, the package will be versioned such that breaking changes are only introduced with a major
version increment. Minor version increments indicate new functionality, and patch increments indicate
bug fixes or minor internal changes.

Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork
the project and submit pull requests. If you're adding a new public API, please also
consider adding [samples][content] that can be turned into a documentation. You might
also want to read the [library design notes][readme] to understand how it works.

The library is available under the Apache v2 license, which allows modification and
redistribution for both commercial and non-commercial purposes. For more information see the
[License file][license] in the GitHub repository.

Build status
------------

This project is automatically built by [Travis CI][travisci] and [AppVeyor][appveyor]. The current status
of the master branch:

 * AppVeyor (Windows): [![Build status](https://ci.appveyor.com/api/projects/status/d74rs3wagh1mxti1/branch/master?svg=true)][appveyor]

 * Travis CI (Mono on Linux): [![Build Status](https://travis-ci.org/Cimpress-MCP/Cimfu.Suave.Healthcheck.svg?branch=master)][travisci]

  [content]: https://github.com/Cimpress-MCP/Cimfu.Suave.Healthcheck/tree/master/docs/content
  [gh]: https://github.com/Cimpress-MCP/Cimfu.Suave.Healthcheck
  [issues]: https://github.com/Cimpress-MCP/Cimfu.Suave.Healthcheck/issues
  [readme]: https://github.com/Cimpress-MCP/Cimfu.Suave.Healthcheck/blob/master/README.md
  [license]: https://github.com/Cimpress-MCP/Cimfu.Suave.Healthcheck/blob/master/LICENSE.txt
  [suave]: http://suave.io
  [hcspec]: https://github.com/Cimpress-MCP/healthcheck.spec
  [travisci]: https://travis-ci.org/Cimpress-MCP/Cimfu.Suave.Healthcheck
  [appveyor]: https://ci.appveyor.com/project/neoeinstein/cimfu-suave-healthcheck/branch/master
  [semver2]: http://semver.org/spec/v2.0.0.html
*)
