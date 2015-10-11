module Cimfu.Suave.Healthcheck.Tests

open NUnit.Framework
open Swensen.Unquote

open Cimfu.Suave.Healthcheck.Internal

[<Test>]
let ``alwaysHealthy healthcheck returns Healthy`` () =
  let expected =
    { testDefaultAggregate with
        Checks = Map.ofList
          [ "noop", testDefaultHealthyData ] }
  let hcMap = Map.ofList ["noop", Checks.alwaysHealthy]
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! expected

[<Test>]
let ``alwaysUnhealthy healthcheck returns Unhealthy`` () =
  let expected =
    { testDefaultAggregate with
        Checks = Map.ofList
          [ "noop", testDefaultUnhealthyData (Some "Server disabled") ] }
  let hcMap = Map.ofList ["noop", Checks.alwaysUnhealthy]
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! expected

let defaultSwitchEnabledExpected =
  { testDefaultAggregate with
      Checks = Map.ofList
        [ "switch", testDefaultHealthyData ] }

let defaultSwitchDisabledExpected =
  { testDefaultAggregate with
      Checks = Map.ofList
        [ "switch", testDefaultUnhealthyData None ] }

[<Test>]
let ``serverMainSwitch is enabled by default`` () =
  let hcMap = Map.ofList ["switch", Checks.serverMainSwitch]
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! defaultSwitchEnabledExpected

[<Test>]
let ``A switch is enabled by default`` () =
  let switch = HealthSwitch.mk None
  let hcMap = Map.ofList ["switch", HealthSwitch.healthcheck switch]
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! defaultSwitchEnabledExpected

[<Test>]
let ``Disabling a switch makes its healthcheck fail`` () =
  let switch = HealthSwitch.mk None
  let hcMap = Map.ofList ["switch", HealthSwitch.healthcheck switch]
  HealthSwitch.disable switch
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! defaultSwitchDisabledExpected

[<Test>]
let ``Disable/Reenable cycle on a switch acts as expected`` () =
  let switch = HealthSwitch.mk None
  let hcMap = Map.ofList ["switch", HealthSwitch.healthcheck switch]
  HealthSwitch.disable switch
  HealthSwitch.enable switch
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! defaultSwitchEnabledExpected
