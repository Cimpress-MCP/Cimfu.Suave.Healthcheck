module Cimfu.Suave.Healthcheck.Tests

open Cimfu.Suave.Healthcheck
open NodaTime
open NUnit.Framework
open Swensen.Unquote

let testTimingSettings =
  { GetTime = fun () -> Instant 0L
    GetTimestamp = fun () -> 0L<stamp>
    StampsPerSecond = 1L<stamp/sec> }

let testDefaultAggregate =
  { GenerationTime = Instant 0L
    Duration = Duration.Zero
    Checks = Map.empty }

let testDefaultHealthyData =
  { TestedAt = Instant 0L
    Duration = Duration.Zero
    Result = HealthcheckResult.Healthy }

let testDefaultUnhealthyData msg =
  { testDefaultHealthyData with
      Result = HealthcheckResult.Unhealthy msg}

let testEvaluateHealthcheck =
  evaluateHealthcheckWith testTimingSettings

let testEvaluateHealthchecks =
  evaluateHealthchecksWith testTimingSettings testEvaluateHealthcheck

[<Test>]
let ``noop healthcheck returns success`` () =
  let expected =
    { testDefaultAggregate with
        Checks = Map.ofList
          [ "noop", testDefaultHealthyData ] }
  let hcMap = Map.ofList ["noop", Checks.noop]
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! expected

let defaultSwitchEnabledExpected =
  { testDefaultAggregate with
      Checks = Map.ofList
        [ "main", testDefaultHealthyData ] }

let defaultSwitchDisabledExpected =
  { testDefaultAggregate with
      Checks = Map.ofList
        [ "main", testDefaultUnhealthyData "Manually disabled" ] }

[<Test>]
let ``DefaultSwitch is enabled by default`` () =
  let hcMap = Map.ofList ["main", Checks.defaultSwitch.Check]
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! defaultSwitchEnabledExpected

[<Test>]
let ``Disabling DefaultSwitch makes healthcheck fail`` () =
  let hcMap = Map.ofList ["main", Checks.defaultSwitch.Check]
  Checks.defaultSwitch.Disable ()
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! defaultSwitchDisabledExpected

[<Test>]
let ``Disable/Reenable cycle on DefaultSwitch acts as expected`` () =
  let hcMap = Map.ofList ["main", Checks.defaultSwitch.Check]
  Checks.defaultSwitch.Disable ()
  Checks.defaultSwitch.Enable ()
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! defaultSwitchEnabledExpected
