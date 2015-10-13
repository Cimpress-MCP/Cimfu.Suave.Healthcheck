module Cimfu.Suave.Healthcheck.Tests

open NUnit.Framework
open Swensen.Unquote

open Cimfu.Suave.Healthcheck.Internal

[<Test>]
let ``alwaysHealthy healthcheck returns Healthy`` () =
  let result = Checks.alwaysHealthy () |> Async.RunSynchronously
  result.Health =! Healthy
  result.Message =! None

[<Test>]
let ``alwaysUnhealthy healthcheck returns Unhealthy`` () =
  let result = Checks.alwaysUnhealthy () |> Async.RunSynchronously
  result.Health =! Unhealthy
  result.Message =! Some "Service unavailable"

[<Test>]
let ``serverMainSwitch is enabled by default`` () =
  let result = Checks.serverMain () |> Async.RunSynchronously
  result.Health =! Healthy
  result.Message =! Some "Server enabled"

[<Test>]
let ``A switch is enabled by default`` () =
  let switch = HealthSwitch(testTimingSettings.GetTime, None, None)
  let result = switch.Check () |> Async.RunSynchronously
  result =! testDefaultHealthyData None

[<Test>]
let ``Disabling a switch makes its healthcheck fail`` () =
  let switch = HealthSwitch(testTimingSettings.GetTime, None, None)
  switch.Disable ()
  let result = switch.Check () |> Async.RunSynchronously
  result =! testDefaultUnhealthyData None

[<Test>]
let ``Disable/Reenable cycle on a switch acts as expected`` () =
  let switch = HealthSwitch(testTimingSettings.GetTime, None, None)
  switch.Disable ()
  switch.Enable ()
  let result = switch.Check () |> Async.RunSynchronously
  result =! testDefaultHealthyData None
