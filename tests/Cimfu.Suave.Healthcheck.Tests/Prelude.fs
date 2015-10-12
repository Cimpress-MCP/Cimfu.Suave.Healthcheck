[<AutoOpen>]
module private Prelude

open Cimfu.Suave.Healthcheck
open Cimfu.Suave.Healthcheck.Timing
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

let testDefaultHealthyData msg =
  { TestedAt = Instant 0L
    Duration = Duration.Zero
    Health = Healthy
    Message = msg }

let testDefaultUnhealthyData msg =
  { testDefaultHealthyData msg with
      Health = Unhealthy }

let testEvaluateHealthchecks =
  evaluateHealthchecksWith testTimingSettings
