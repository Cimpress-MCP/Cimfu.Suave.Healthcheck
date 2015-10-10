(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Cimfu.Suave.Healthcheck"

(**
Cimfu.Suave.Healthcheck
======================

Documentation

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

This example demonstrates attaching a noop healthcheck to a Suave `WebPart`.

*)
#r "Suave.dll"
#r "Cimfu.Suave.Healthcheck.dll"
open Suave
open Suave.Types
open Cimfu.Suave.Healthcheck

let myApp : WebPart = Http.RequestErrors.NOT_FOUND "Not here"

let healthchecks = Map.ofList ["noop", Checks.noop]

let app =
  myApp
  |> withHealthcheck healthchecks

(**
Some more info

Samples & documentation
-----------------------

The library comes with comprehensible documentation. 
It can include tutorials automatically generated from `*.fsx` files in [the content folder][content]. 
The API reference is automatically generated from Markdown comments in the library implementation.

 * [Tutorial](tutorial.html) contains a further explanation of this sample library.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.
 
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
  [travisci]: https://travis-ci.org/Cimpress-MCP/Cimfu.Suave.Healthcheck
  [appveyor]: https://ci.appveyor.com/project/neoeinstein/cimfu-suave-healthcheck/branch/master
*)
