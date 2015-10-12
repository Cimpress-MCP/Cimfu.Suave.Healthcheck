/// A module providing service healthcheck functions for a Suave application.
module Cimfu.Suave.Healthcheck

open Chiron
open Chiron.Operators
open Suave.Types
open Suave.Http
open Suave.Http.Applicatives
open Suave.Http.Writers
open NodaTime

module Timing =
  /// Represents one stopwatch timestamp (as reported by `System.Diagnostics.Stopwatch`).
  type [<Measure>] stamp
  /// Represents one second.
  type [<Measure>] sec

  /// Timing information. Specifying custom values is helpful for testing purposes.
  type TimingSettings =
    { /// Gets the current time.
      GetTime : unit -> Instant
      /// Get the current timestamp.
      GetTimestamp : unit -> int64<stamp>
      /// Indicates the frequency of timestamps per second.
      StampsPerSecond : int64<stamp/sec> }
    /// A default set of timing information. For common use cases, this can be used
    /// without modification.
    static member Default =
      { GetTime = fun () -> NodaTime.SystemClock.Instance.Now
        GetTimestamp = System.Diagnostics.Stopwatch.GetTimestamp >> LanguagePrimitives.Int64WithMeasure
        StampsPerSecond = System.Diagnostics.Stopwatch.Frequency |> LanguagePrimitives.Int64WithMeasure }

open Timing

[<AutoOpen>]
module private Time =
  /// Represents one clock tick (as reported by `NodaTime`).
  type [<Measure>] tick
  /// Represents one millisecond.
  type [<Measure>] ms

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

  let isoDateTimePattern = Text.InstantPattern.Create("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'", System.Globalization.CultureInfo.InvariantCulture)

  let inline toIsoDateTime i = isoDateTimePattern.Format i
  let inline tryAsIsoDateTime str =
    let parse = isoDateTimePattern.Parse(str)
    if parse.Success then
      Some parse.Value
    else
      None

  [<RequireQualifiedAccess>]
  module Json =
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
    let inline ofDurationAsMs d =
      ((Duration.toMs d) / 1M<ms> * 10M |> round) / 10M |> Number

    let toInstant json =
      match json with
      | String x ->
          match tryAsIsoDateTime x with
          | Some i -> Value i
          | None -> Error (sprintf "Unable to parse %s as an ISO-8601 datetime" x)
      | _ -> Error (sprintf "Unable to parse %A as an ISO-8601 datetime" json)
    let inline ofInstant i =
      String <| toIsoDateTime i

/// Represents the health of a service.
type Health =
  /// Indicates that the healthcheck has passed.
  | Healthy
  /// Indicates that the healthcheck has failed and that the service should be marked unhealthy.
  | Unhealthy
  /// Adds together two health values such that the result
  /// will be `Healthy` if and only if both operands are `Healthy`.
  /// Otherwise the result will be `Unhealthy`.
  static member (+) (l, r) =
    match l, r with
    | Healthy, Healthy -> Healthy
    | _-> Unhealthy

/// The result of running a healthcheck; combines a `Health` with
/// an optional status message.
type HealthcheckResult =
  { /// The time at which the healthcheck result was returned.
    TestedAt : Instant
    /// The duration required to evaluate this healthcheck.
    Duration : Duration
    /// The service status according to the healthcheck.
    Health : Health
    /// An optional message describing the state of the healthcheck or providing
    /// additional diagnostic information.
    Message : string option }

/// The function signature for a healthcheck: `unit -> Async<HealthcheckResult>`.
type Healthcheck = unit -> Async<HealthcheckResult>

/// Represents a health status without any timing context.
type HealthStatus =
  | HealthStatus of health : Health * message : string option

/// The function signature for a health evaluator: `unit -> Async<HealthStatus>`.
/// Conversion of a `HealthEvaluator` to a `Healthcheck` can be done by
/// using the `evaluateToHealthcheck` function.
type HealthEvaluator = unit -> Async<HealthStatus>

type Health with
  /// Serializes a `Health` to a `Chiron.Json`.
  static member ToJson (x:Health) =
    match x with
    | Healthy -> ToJsonDefaults.ToJson "passed"
    | Unhealthy -> ToJsonDefaults.ToJson "failed"

  /// Deserializes a `Health` from a `Chiron.Json`.
  static member FromJson (_:Health) =
    function
    | String "passed" as json -> Json.init Healthy json
    | String "failed" as json -> Json.init Unhealthy json
    | json -> Json.error (sprintf "couldn't deserialize %A to Health" json) json

type HealthcheckResult with
  /// Serializes a `HealthcheckResult` to a `Chiron.Json`.
  static member ToJson (x:HealthcheckResult) =
       Json.writeWith Json.ofDurationAsMs  "duration_millis" x.Duration
    *> Json.writeWith Json.ofInstant "tested_at" x.TestedAt
    *> Json.write "result" x.Health
    *> Json.writeUnlessDefault "message" None x.Message

  /// Deserializes a `HealthcheckResult` from a `Chiron.Json`.
  static member FromJson (_:HealthcheckResult) =
    fun d h m tt -> { Duration = d; Health = h; Message = m; TestedAt = tt }
    <!> Json.readWith Json.toDurationAsMs "duration_millis"
    <*> Json.read "result"
    <*> Json.readOrDefault "message" None
    <*> Json.readWith Json.toInstant "tested_at"

/// A switch associated with a healthcheck which can be set to
/// return `Healthy` when enabled and `Unhealthy` when disabled.
type HealthSwitch private (getTime : unit -> Instant, enabledMsg : string option, disabledMsg: string option, dummy : unit) =
  let createResult newHealth newMsg =
    { TestedAt = getTime ()
      Duration = Duration.Zero
      Health = newHealth
      Message = newMsg }
  let mutable result = createResult Healthy enabledMsg

  new(getTime, enabledMsg, disabledMsg) = HealthSwitch(getTime, enabledMsg, disabledMsg, ())
  new(enabledMsg, disabledMsg) = HealthSwitch(TimingSettings.Default.GetTime, enabledMsg, disabledMsg)
  new(disabledMsg) = HealthSwitch(None, Some disabledMsg)
  new() = HealthSwitch("Service disabled")

  member __.Check : Healthcheck = fun () -> async.Return result
  member __.Enable () = result <- createResult Healthy enabledMsg
  member __.Enable msg = result <- createResult Healthy msg
  member __.Disable () = result <- createResult Unhealthy disabledMsg
  member __.Disable msg = result <- createResult Unhealthy msg

  static member ServerMain =
    HealthSwitch(Some "Server enabled", Some "Server disabled")

/// A set of core healthchecks.
[<RequireQualifiedAccess>]
module Checks =
  let healthyResult = { TestedAt = TimingSettings.Default.GetTime (); Duration = Duration.Zero; Health = Healthy; Message = None }
  /// A simple healthcheck that will always report `Healthy`.
  let alwaysHealthy : Healthcheck = fun () -> async.Return healthyResult
  let unhealthyResult = { healthyResult with Health = Unhealthy; Message = Some "Service unavailable" }
  /// A simple healthcheck that will always report `Unhealthy`. Including this
  /// healthcheck will cause the service to always report as `Unhealthy`.
  let alwaysUnhealthy : Healthcheck = fun () -> async.Return unhealthyResult
  /// A healthcheck that will report status of the main server switch provided
  /// by `HealthSwitch.ServerMain`.
  let serverMain = HealthSwitch.ServerMain.Check

/// Several internal functions that are not commonly used by consumers of this library.
module Internal =
  /// Represents an aggregate result of several evaluated healthchecks.
  type AggregateHealthcheckResult =
    { /// The time at which all healthchecks completed and were aggregated.
      GenerationTime : Instant
      /// The duration required to evaluate the aggregated healthchecks.
      Duration : Duration
      /// A map of healthcheck results by their configured key.
      Checks : Map<string, HealthcheckResult> }

  type AggregateHealthcheckResult with
    /// Serializes an `AggregateHealthcheckResult` to a `Chiron.Json`.
    static member ToJson (x:AggregateHealthcheckResult) =
         Json.writeWith Json.ofInstant "generated_at" x.GenerationTime
      *> Json.writeWith Json.ofDurationAsMs "duration_millis" x.Duration
      *> Json.write "tests" x.Checks

    /// Deserializes an `AggregateHealthcheckResult` from a `Chiron.Json`.
    static member FromJson (_:AggregateHealthcheckResult) =
      fun g d c -> { GenerationTime = g; Duration = d; Checks = c }
      <!> Json.readWith Json.toInstant "generated_at"
      <*> Json.readWith Json.toDurationAsMs "duration_millis"
      <*> Json.read "tests"

  /// Asynchronously evaluates a healthcheck and associates the resulting data with its key.
  let inline evaluateMappedHealthcheck (k, hc : Healthcheck) =
    async {
      let! result = hc ()
      return (k, result)
    }

  /// Asynchronously evaluates a set of healthchecks with the specified
  /// timing settings and healthcheck evaluator.
  let evaluateHealthchecksWith tsSettings hcMap =
    async {
      let startTime = tsSettings.GetTimestamp ()
      let! results = Async.Parallel(Map.toSeq hcMap |> Seq.map evaluateMappedHealthcheck)
      let testTime = tsSettings.GetTime ()
      let stopTime = tsSettings.GetTimestamp ()

      return
        { GenerationTime = testTime
          Duration = (stopTime - startTime) |> Duration.ofTimestampDiff tsSettings.StampsPerSecond
          Checks = Map.ofArray results }
    }

  /// A `Suave` `WebPart` which will evaluate a set of healthchecks using
  /// the specified evaluator.
  let doHealthcheckWith getResults healthchecks : WebPart =
    fun ctx -> async {
      let! result = getResults healthchecks

      let aggResult =
        result.Checks
        |> Map.fold (fun s _ d -> s + d.Health) Healthy

      let status =
          match aggResult with
          | Healthy -> Successful.OK
          | Unhealthy -> ServerErrors.SERVICE_UNAVAILABLE

      return! status (Json.serialize result |> Json.format) ctx
    }

  /// Only allows GET or HEAD requests to be passed on to `app`. Handles all
  /// other requests by responding with `405 METHOD NOT ALLOWED`.
  let inline allowGetOrHeadOnly (app : WebPart) : WebPart =
    choose
      [ choose [ GET; HEAD ] >>= app
        setHeader "Allow" "GET, HEAD" >>= RequestErrors.METHOD_NOT_ALLOWED "" ]

  /// Evaluates the healthchecks specified in `hcMap` and attaches the appropriate
  /// headers onto the response.
  let inline healthcheckHandler doHealthcheckWith hcMap : WebPart =
    doHealthcheckWith hcMap
    >>= setMimeType "application/json"
    >>= setHeader "Cache-Control" "no-cache"

  /// Handles healthcheck requests to the `healthcheckRoot` with the specified
  /// `healthcheckHandler`
  let inline handleHealthcheckWith healthcheckHandler healthcheckRoot : WebPart =
    path healthcheckRoot >>= allowGetOrHeadOnly healthcheckHandler

  /// Asynchronously maps a `HealthEvaluator` and to a `HealthcheckResult`
  /// using the timing settings from `tsSettings`.
  let evaluateToHealthcheckWith tsSettings (he : HealthEvaluator) =
    async {
      let startTime = tsSettings.GetTimestamp ()
      let! (HealthStatus (health, msg)) = he ()
      let testTime = tsSettings.GetTime ()
      let stopTime = tsSettings.GetTimestamp ()
      return
        { TestedAt = testTime
          Health = health
          Message = msg
          Duration = (stopTime - startTime) |> Duration.ofTimestampDiff tsSettings.StampsPerSecond }
    }

open Internal

/// Asynchronously maps a `HealthEvaluator` and to a `HealthcheckResult`.
let inline evaluateToHealthcheck he =
  evaluateToHealthcheckWith TimingSettings.Default he

/// Asynchronously evaluates a set of healthchecks.
let inline evaluateHealthchecks hcMap =
  evaluateHealthchecksWith TimingSettings.Default hcMap

/// Evaluates a specified set of healthchecks and returns a new
/// `HttpContext` representing the serialized result.
let inline doHealthcheck hcMap =
  doHealthcheckWith evaluateHealthchecks hcMap

/// The default healthcheck root path for handlers whose
/// path isn't specified: `/healthcheck`.
let [<Literal>] DefaultHealthcheckRoot = "/healthcheck"

/// Handles healthcheck requests at `healthcheckRoot`
let inline handleHealthcheckAt healthcheckRoot hcMap =
  handleHealthcheckWith (healthcheckHandler doHealthcheck hcMap) healthcheckRoot

/// Handles healthcheck requests
let inline handleHealthcheck hcMap =
  handleHealthcheckAt DefaultHealthcheckRoot hcMap

/// Attaches a healthcheck endpoint as a prefix to a `Suave` `WebPart`
/// handling all requests for paths starting with `rootPath`.
///
/// An `app` may be fluently pipelined into this function.
let inline prefixWithHealthcheckAt healthcheckRoot hcMap app =
  choose
    [ handleHealthcheckAt healthcheckRoot hcMap
      app ]

/// Attaches a healthcheck endpoint as a prefix to a `Suave` `WebPart`
/// handling all requests for paths starting with "/healthcheck".
///
/// An `app` may be fluently pipelined into this function.
let inline prefixWithHealthcheck hcMap app =
  prefixWithHealthcheckAt DefaultHealthcheckRoot hcMap app
