(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Cimfu.Suave.Healthcheck"

#r "Aether.dll"
#r "Chiron.dll"
#r "Suave.dll"
#r "Cimfu.Suave.Healthcheck.dll"
open Suave.Http
open Cimfu.Suave.Healthcheck

let myApp = RequestErrors.NOT_FOUND "Not here"

(**
Tutorial
========================

This tutorial is not yet complete, but here is an example of using the healthcheck functionality with `Suave`.
In each of these examples, it is presumed that your existing `Suave` application is bound to `myApp`.
*)
let healthchecks = Map.ofList ["main", Checks.serverMainSwitch]

let app =
  myApp
  |> withHealthcheck healthchecks

(** You can also choose the endpoint at which you would like your healthchecks reported, as in the following example: *)
let appAtAltAddress =
  myApp
  |> withHealthcheckAt "/livecheck" healthchecks

(**
By using `withHealthcheck` or `withHealthcheckAt`, the healthcheck functions will attach as a prefix to `myApp`.
This means that if there is any request to the healthcheck endpoint, it will be handled by the healthcheck service. Requests
to this endpoint that are invalid (such as `POST`s) will be handled by returning an appropriate response (such as a `504 METHOD NOT ALLOWED`)
instead of passing the context to `myApp`.

In this way, you can choose to attach multiple healthchecks for services hosted by the same instance:
*)
let service1Hc = Map.ofList ["main", Checks.serverMainSwitch; "redis", Checks.alwaysHealthy]
let service2Hc = Map.ofList ["main", Checks.serverMainSwitch; "unreliable", Checks.alwaysUnhealthy]

let appWithTwoServices =
  myApp
  |> withHealthcheckAt "/service-1/livecheck" service1Hc
  |> withHealthcheckAt "/service-2/livecheck" service2Hc

(**
Healthcheck evaluation
-----------------------

When a healthcheck is evaluated it will return a result indicating the current health of the service: *)
let evaluateHc = evaluateHealthcheck Checks.serverMainSwitch
let result = evaluateHc |> Async.RunSynchronously

(*** include-value: result ***)
(** If we then disable the main server switch and re-evaluate the healthcheck that check will now evaluate to `Unhealthy`.*)
HealthSwitch.disable HealthSwitch.serverMain
let newResult = evaluateHc |> Async.RunSynchronously

(*** include-value: newResult ***)
(*** hide ***)
HealthSwitch.enable HealthSwitch.serverMain

(** The evaluation of a set of healthchecks together creates an aggregate healthcheck result: *)
let evaluateHcs =
  Map.ofList
    [ "main", Checks.serverMainSwitch
      "redis", Checks.alwaysHealthy
      "unreliable", Checks.alwaysUnhealthy ]
  |> evaluateHealthchecks
let aggregateResult = evaluateHcs |> Async.RunSynchronously

(*** include-value: aggregateResult ***)
(** This result gets serialized to JSON and returned to the client with the appropriate HTTP status *)
(*** hide ***)
open Chiron
let serializedResult = Json.serialize aggregateResult |> Json.format

(*** include-value: serializedResult ***)
