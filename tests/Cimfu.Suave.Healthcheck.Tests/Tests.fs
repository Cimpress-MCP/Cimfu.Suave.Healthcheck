module Cimfu.Suave.Healthcheck.Tests

open NUnit.Framework
open Swensen.Unquote

open Cimfu.Suave.Healthcheck.Internals

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
        [ "main", testDefaultUnhealthyData "Server manually disabled" ] }

[<Test>]
let ``DefaultSwitch is enabled by default`` () =
  let hcMap = Map.ofList ["main", Checks.serverMainSwitch]
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! defaultSwitchEnabledExpected

[<Test>]
let ``Disabling DefaultSwitch makes healthcheck fail`` () =
  let hcMap = Map.ofList ["main", Checks.serverMainSwitch]
  HealthSwitch.disable HealthSwitch.serverMain
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! defaultSwitchDisabledExpected

[<Test>]
let ``Disable/Reenable cycle on DefaultSwitch acts as expected`` () =
  let hcMap = Map.ofList ["main", Checks.serverMainSwitch]
  HealthSwitch.disable HealthSwitch.serverMain
  HealthSwitch.enable HealthSwitch.serverMain
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! defaultSwitchEnabledExpected
