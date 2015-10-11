[<AutoOpen>]
module private Prelude

open Cimfu.Suave.Healthcheck
open Cimfu.Suave.Healthcheck.Internal
open NodaTime

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
      Result = { Status = Unhealthy; Message = msg } }

let testEvaluateHealthcheck =
  evaluateHealthcheckWith testTimingSettings

let testEvaluateHealthchecks =
  evaluateHealthchecksWith testTimingSettings testEvaluateHealthcheck
