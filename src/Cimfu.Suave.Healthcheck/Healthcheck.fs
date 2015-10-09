module Cimfu.Suave.Healthcheck

open Chiron
open Chiron.Operators
open Suave.Types
open Suave.Http
open Suave.Http.Applicatives
open Suave.Http.Writers
open NodaTime

type [<Measure>] tick
type [<Measure>] ms
type [<Measure>] stamp
type [<Measure>] sec

type TimingSettings =
  { GetTime : unit -> Instant
    GetTimestamp : unit -> int64<stamp>
    StampsPerSecond : int64<stamp/sec> }
  static member Default =
    { GetTime = fun () -> NodaTime.SystemClock.Instance.Now
      GetTimestamp = System.Diagnostics.Stopwatch.GetTimestamp >> LanguagePrimitives.Int64WithMeasure
      StampsPerSecond = System.Diagnostics.Stopwatch.Frequency |> LanguagePrimitives.Int64WithMeasure }

[<AutoOpen>]
module private Time =
  let ticksPerMs = (decimal NodaConstants.TicksPerMillisecond) * 1M<tick/ms>
  let ticksPerSec = (decimal NodaConstants.TicksPerSecond) * 1M<tick/sec>
  let toDecimalWithMeasure (x : int64<'u>) : decimal<'u> = x |> decimal |> LanguagePrimitives.DecimalWithMeasure

  module Ticks =
    let inline toMs ticks =
      ticks / ticksPerMs

  module Duration =
    let inline toTicks (d : Duration) =
      1M<tick> * (decimal d.Ticks)
    let inline toMs (d : Duration) =
      d |> toTicks |> Ticks.toMs
    let inline ofTicks (n : decimal<tick>) =
      n / 1M<tick> |> int64 |> Duration.FromTicks
    let inline ofMs (n : decimal<ms>) =
      n * ticksPerMs |> ofTicks
    let inline ofTimestampDiff (stampsPerSecond : int64<stamp/sec>) (tsDiff : int64<stamp>) =
        (toDecimalWithMeasure tsDiff) * ticksPerSec / (toDecimalWithMeasure stampsPerSecond) |> ofTicks

  let isoDateTimePattern = Text.InstantPattern.Create("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'z'", System.Globalization.CultureInfo.InvariantCulture)

  let inline toIsoDateTime i = isoDateTimePattern.Format i
  let inline tryAsIsoDateTime str =
    let parse = isoDateTimePattern.Parse(str)
    if parse.Success then
      Some parse.Value
    else
      None     

[<RequireQualifiedAccess>]
module private Json =
  let toDurationAsMs json =
    let handle v =
      Duration.ofMs (v * 1M<ms>) |> Value
    match json with
    | Number v -> handle v
    | String str ->
      match System.Decimal.TryParse str with
      | true, v -> handle v
      | _ -> Error (sprintf "Unable to parse %s as a number" str)
    | _ -> Error (sprintf "Unable to parse %A as a number" json)
  let ofDurationAsMs d =
    ((Duration.toMs d) / 1M<ms> * 10M |> round) / 10M |> Number

  let toInstant json =
    match json with
    | String x ->
        match tryAsIsoDateTime x with
        | Some i -> Value i
        | None -> Error (sprintf "Unable to parse %s as an ISO-8601 datetime" x)
    | _ -> Error (sprintf "Unable to parse %A as an ISO-8601 datetime" json)
  let ofInstant i =
    String <| toIsoDateTime i

type HealthcheckStatus =
  | Healthy
  | Unhealthy

type HealthcheckResult =
  { Status : HealthcheckStatus
    Message : string option }
  static member Healthy = { Status = Healthy; Message = None }
  static member Unhealthy message = { Status = Unhealthy; Message = Some message }

module Switches =
  type HealthSwitch =
    { Enable : unit -> unit
      Disable : unit -> unit
      Check : unit -> Async<HealthcheckResult> }

  let mk () =
    let mutable enabled = true
    { Enable = fun () -> enabled <- true
      Disable = fun () -> enabled <- false
      Check = fun () ->
        if enabled then HealthcheckResult.Healthy
        else HealthcheckResult.Unhealthy "Manually disabled"
        |> async.Return }

[<RequireQualifiedAccess>]
module Checks =
  let noop = fun () -> async.Return HealthcheckResult.Healthy
  let defaultSwitch = Switches.mk ()
 
let merge l r =
  match l, r with
  | Unhealthy, _
  | _, Unhealthy -> Unhealthy
  | _-> Healthy

type HealthcheckStatus with
  static member ToJson (x:HealthcheckStatus) =
    match x with
    | Healthy -> ToJsonDefaults.ToJson "passed"
    | Unhealthy -> ToJsonDefaults.ToJson "failed"

  static member FromJson (_:HealthcheckStatus) =
    function
    | String "passed" as json -> Json.init Healthy json
    | String "failed" as json -> Json.init Unhealthy json
    | json -> Json.error (sprintf "couldn't deserialize %A to HealthcheckStatus" json) json

type HealthcheckData =
  { Duration : Duration
    Result : HealthcheckResult
    TestedAt : Instant }

type AggregateHealthcheckResult =
  { GenerationTime : Instant
    Duration : Duration
    Checks : Map<string, HealthcheckData> }

type HealthcheckData with
  static member ToJson (x:HealthcheckData) =
       Json.writeWith Json.ofDurationAsMs  "duration_millis" x.Duration
    *> Json.writeWith Json.ofInstant "tested_at" x.TestedAt
    *> Json.write "result" x.Result.Status
    *> Json.writeUnlessDefault "message" None x.Result.Message

  static member FromJson (_:HealthcheckData) =
    fun d r m tt -> { Duration = d; Result = { Status = r; Message = m }; TestedAt = tt }
    <!> Json.readWith Json.toDurationAsMs "duration_millis"
    <*> Json.read "result"
    <*> Json.readOrDefault "message" None
    <*> Json.readWith Json.toInstant "tested_at"

type AggregateHealthcheckResult with
  static member ToJson (x:AggregateHealthcheckResult) =
       Json.writeWith Json.ofInstant "generated_at" x.GenerationTime
    *> Json.writeWith Json.ofDurationAsMs "duration_millis" x.Duration
    *> Json.write "tests" x.Checks

  static member FromJson (_:AggregateHealthcheckResult) =
    fun g d c -> { GenerationTime = g; Duration = d; Checks = c }
    <!> Json.readWith Json.toInstant "generated_at"
    <*> Json.readWith Json.toDurationAsMs "duration_millis"
    <*> Json.read "checks"

let evaluateHealthcheckWith tsSettings hc =
  async {
    let startTime = tsSettings.GetTimestamp ()
    let! result = hc ()
    let testTime = tsSettings.GetTime ()
    let stopTime = tsSettings.GetTimestamp ()
    return
      { TestedAt = testTime
        Result = result
        Duration = (stopTime - startTime) |> Duration.ofTimestampDiff tsSettings.StampsPerSecond}
  }

let evaluateMappedHealthcheck evaluateHealthcheck (k, v) =
  async {
    let! newV = evaluateHealthcheck v
    return (k, newV)
  }

let evaluateHealthchecksWith tsSettings evaluateHealthcheck hcMap =
  async {
    let startTime = tsSettings.GetTimestamp ()
    let! results = Async.Parallel(Map.toSeq hcMap |> Seq.map (evaluateMappedHealthcheck evaluateHealthcheck))
    let testTime = tsSettings.GetTime ()
    let stopTime = tsSettings.GetTimestamp ()

    return
      { GenerationTime = testTime
        Duration = (stopTime - startTime) |> Duration.ofTimestampDiff tsSettings.StampsPerSecond
        Checks = Map.ofArray results }
  }

let doHealthcheckWith evaluateHealthchecks healthchecks : WebPart =
  fun ctx -> async {
    let! result = evaluateHealthchecks healthchecks

    let aggResult =
      result.Checks
      |> Map.fold (fun s _ d -> merge s d.Result.Status) Unhealthy

    let status =
        match aggResult with
        | Healthy -> Successful.OK
        | Unhealthy -> ServerErrors.SERVICE_UNAVAILABLE

    return! status (Json.serialize result |> Json.format) ctx
  } >>= setMimeType "application/json"
    >>= setHeader "Cache-Control" "no-cache"

let withHealthcheckWith doHealthcheckWith healthchecks app : WebPart =
  choose
    [ path "/healthcheck" >>= choose
        [ choose [ GET; HEAD ] >>= doHealthcheckWith healthchecks
          setHeader "Allow" "GET, HEAD" >>= RequestErrors.METHOD_NOT_ALLOWED "" ]
      app ]

let evaluateHealthcheck hc =
  evaluateHealthcheckWith TimingSettings.Default hc

let evaluateHealthchecks hcMap =
  evaluateHealthchecksWith TimingSettings.Default evaluateHealthcheck hcMap

let doHealthcheck healthchecks =
  doHealthcheckWith evaluateHealthchecks healthchecks

let withHealthcheck healthchecks app =
  withHealthcheckWith doHealthcheck healthchecks app
