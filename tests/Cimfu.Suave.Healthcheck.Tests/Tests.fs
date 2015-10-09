module Cimfu.Suave.Healthcheck.Tests

open Cimfu.Suave.Healthcheck
open NUnit.Framework
open Swensen.Unquote

let testTimingSettings =
  { GetTime = fun () -> NodaTime.Instant 0L
    GetTimestamp = fun () -> 0L<stamp>
    StampsPerSecond = 1L<stamp/sec> }

let testEvaluateHealthcheck =
  evaluateHealthcheckWith testTimingSettings

let testEvaluateHealthchecks =
  evaluateHealthchecksWith testTimingSettings testEvaluateHealthcheck

[<Test>]
let ``noop healthcheck returns success`` () =
  let expected =
    { GenerationTime = NodaTime.Instant 0L
      Duration = NodaTime.Duration.Zero
      Checks = Map.ofList
        [ "noop", { TestedAt = NodaTime.Instant 0L
                    Duration = NodaTime.Duration.Zero
                    Result = HealthcheckResult.Healthy } ] }
  let hcMap = Map.ofList ["noop", Checks.noop]
  let actual = testEvaluateHealthchecks hcMap |> Async.RunSynchronously
  actual =! expected
