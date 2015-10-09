(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Cimfu.Suave.Healthcheck"

(**
Tutorial
========================

This tutorial is not yet complete, but here is an example of using the healthcheck functionality with Suave.

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

*)
