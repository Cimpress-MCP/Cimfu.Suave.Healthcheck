/// A module providing service healthcheck functions for a Suave application.
module Cimfu.Suave.Healthcheck

open Chiron
open Chiron.Operators
open Suave.Types
open Suave.Http
open Suave.Http.Applicatives
open Suave.Http.Writers
open NodaTime

/// A status representing the success or failure of a particular healthcheck.
type HealthcheckStatus =
  /// Indicates that the healthcheck has passed.
  | Healthy
  /// Indicates that the healthcheck has failed and that the service should be marked unhealthy.
  | Unhealthy
  /// Adds together two healthcheck statuses such that the result
  /// will be `Healthy` if and only if both operands are `Healthy`.
  /// Otherwise the result will be `Unhealthy`.
  static member (+) (l, r) =
    match l, r with
    | Unhealthy, _
    | _, Unhealthy -> Unhealthy
    | _-> Healthy

/// The result of running a healthcheck; combines a `HealthcheckStatus` with
/// an optional status message.
type HealthcheckResult =
  { /// The service status according to the healthcheck.
    Status : HealthcheckStatus
    /// An optional message describing the state of the healthcheck or providing
    /// additional diagnostic information.
    Message : string option }
  /// The default `Healthy` result with no attached message.
  static member Healthy = { Status = Healthy; Message = None }
  /// Creates an `Unhealthy` result with a specified message.
  static member Unhealthy message = { Status = Unhealthy; Message = Some message }

/// The function signature for a healthcheck: `unit -> Async<HealthcheckResult>`.
type Healthcheck = unit -> Async<HealthcheckResult>

type HealthcheckStatus with
  /// Serializes a `HealthcheckStatus` to a `Chiron.Json`.
  static member ToJson (x:HealthcheckStatus) =
    match x with
    | Healthy -> ToJsonDefaults.ToJson "passed"
    | Unhealthy -> ToJsonDefaults.ToJson "failed"

  /// Deserializes a `HealthcheckStatus` from a `Chiron.Json`.
  static member FromJson (_:HealthcheckStatus) =
    function
    | String "passed" as json -> Json.init Healthy json
    | String "failed" as json -> Json.init Unhealthy json
    | json -> Json.error (sprintf "couldn't deserialize %A to HealthcheckStatus" json) json

/// A switch associated with a healthcheck which can be set to
/// return `Healthy` when enabled and `Unhealthy` when disabled.
type HealthSwitch =
  private
    { Enable : unit -> unit
      Disable : unit -> unit
      Check : Healthcheck }

/// A set of functions for operating with the `HealthSwitch` type.
[<RequireQualifiedAccess;CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HealthSwitch =
  /// Creates a switch which, when disabled, will report the provided `disabledMessage`.
  let mk disabledMessage =
    let mutable enabled = true
    { Enable = fun () -> enabled <- true
      Disable = fun () -> enabled <- false
      Check = fun () ->
        if enabled then HealthcheckResult.Healthy
        else { Status = Unhealthy; Message = disabledMessage }
        |> async.Return }
  /// Returns the healthcheck associated with a switch.
  let healthcheck hs =
    hs.Check
  /// Enables the switch so that it will report `Healthy` during a healthcheck.
  let enable hs =
    hs.Enable ()
  /// Disables the switch so that it will report `Unhealthy` during a healthcheck.
  let disable hs =
    hs.Disable ()
  /// Main default switch for the whole server. Enabling or disabling this swich
  /// will cause `Checks.serverMainSwitch` to report `Healthy` or `Unhealthy`
  /// respectively.
  let serverMain = mk <| Some "Server manually disabled"

/// A set of core healthchecks.
[<RequireQualifiedAccess>]
module Checks =
  /// A simple healthcheck that will always report `Healthy`.
  let alwaysHealthy = fun () -> async.Return HealthcheckResult.Healthy
  /// A simple healthcheck that will always report `Unhealthy`. Including this
  /// healthcheck will cause the service to always report as `Unhealthy`.
  let alwaysUnhealthy = fun () -> async.Return (HealthcheckResult.Unhealthy "Server disabled")
  /// A healthcheck that will report status of the main server switch provided
  /// by `HealthSwitch.serverMain`.
  let serverMainSwitch = HealthSwitch.healthcheck HealthSwitch.serverMain

/// Several internal functions that are not commonly used by consumers of this library.
module Internal =
  /// Represents one clock tick (as reported by `NodaTime`).
  type [<Measure>] tick
  /// Represents one millisecond.
  type [<Measure>] ms
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

    let isoDateTimePattern = Text.InstantPattern.Create("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'", System.Globalization.CultureInfo.InvariantCulture)

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

  /// Represents the context of an evaluated healthcheck.
  type HealthcheckData =
    { /// The time at which the healthcheck result was returned.
      TestedAt : Instant
      /// The duration required to evaluate this healthcheck.
      Duration : Duration
      /// The reported healthcheck result.
      Result : HealthcheckResult }

  /// Represents an aggregate result of several evaluated healthchecks.
  type AggregateHealthcheckResult =
    { /// The time at which all healthchecks completed and were aggregated.
      GenerationTime : Instant
      /// The duration required to evaluate the aggregated healthchecks.
      Duration : Duration
      /// A map of healthcheck results by their configured key.
      Checks : Map<string, HealthcheckData> }

  type HealthcheckData with
    /// Serializes a `HealthcheckData` to a `Chiron.Json`.
    static member ToJson (x:HealthcheckData) =
         Json.writeWith Json.ofDurationAsMs  "duration_millis" x.Duration
      *> Json.writeWith Json.ofInstant "tested_at" x.TestedAt
      *> Json.write "result" x.Result.Status
      *> Json.writeUnlessDefault "message" None x.Result.Message

    /// Deserializes a `HealthcheckData` from a `Chiron.Json`.
    static member FromJson (_:HealthcheckData) =
      fun d r m tt -> { Duration = d; Result = { Status = r; Message = m }; TestedAt = tt }
      <!> Json.readWith Json.toDurationAsMs "duration_millis"
      <*> Json.read "result"
      <*> Json.readOrDefault "message" None
      <*> Json.readWith Json.toInstant "tested_at"

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
      <*> Json.read "checks"

  /// Asynchronously evaluates a healthcheck with the specified timing settings.
  let evaluateHealthcheckWith tsSettings (hc : Healthcheck) =
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

  /// Asynchronously evaluates a healthcheck and associates the resulting data with its key.
  let inline evaluateMappedHealthcheck evaluateHealthcheck (k, v) =
    async {
      let! newV = evaluateHealthcheck v
      return (k, newV)
    }

  /// Asynchronously evaluates a set of healthchecks with the specified
  /// timing settings and healthcheck evaluator.
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

  /// A `Suave` `WebPart` which will evaluate a set of healthchecks using
  /// the specified evaluator.
  let doHealthcheckWith evaluateHealthchecks healthchecks : WebPart =
    fun ctx -> async {
      let! result = evaluateHealthchecks healthchecks

      let aggResult =
        result.Checks
        |> Map.fold (fun s _ d -> s + d.Result.Status) Healthy

      let status =
          match aggResult with
          | Healthy -> Successful.OK
          | Unhealthy -> ServerErrors.SERVICE_UNAVAILABLE

      return! status (Json.serialize result |> Json.format) ctx
    } >>= setMimeType "application/json"
      >>= setHeader "Cache-Control" "no-cache"

  /// Attaches a healthcheck endpoint as a prefix to a `Suave` `WebPart`
  /// handling all requests beginning with `rootPath`. Healthchecks are
  /// evaluated using `doHealthcheckWith`.
  ///
  /// An `app` may be fluently pipelined into this function.
  let withHealthcheckWith doHealthcheckWith rootPath healthchecks app : WebPart =
    choose
      [ path rootPath >>= choose
          [ choose [ GET; HEAD ] >>= doHealthcheckWith healthchecks
            setHeader "Allow" "GET, HEAD" >>= RequestErrors.METHOD_NOT_ALLOWED "" ]
        app ]

open Internal

/// Asynchronously evaluates a healthcheck.
let inline evaluateHealthcheck hc =
  evaluateHealthcheckWith TimingSettings.Default hc

/// Asynchronously evaluates a set of healthchecks.
let inline evaluateHealthchecks hcMap =
  evaluateHealthchecksWith TimingSettings.Default evaluateHealthcheck hcMap

/// Evaluates a specified set of healthchecks and returns a new
/// `HttpContext` representing the serialized result.
let inline doHealthcheck hcMap =
  doHealthcheckWith evaluateHealthchecks hcMap

/// Attaches a healthcheck endpoint as a prefix to a `Suave` `WebPart`
/// handling all requests for paths starting with `rootPath`.
///
/// An `app` may be fluently pipelined into this function.
let inline withHealthcheckAt rootPath hcMap app =
  withHealthcheckWith doHealthcheck rootPath hcMap app

/// Attaches a healthcheck endpoint as a prefix to a `Suave` `WebPart`
/// handling all requests for paths starting with "/healthcheck".
///
/// An `app` may be fluently pipelined into this function.
let inline withHealthcheck hcMap app =
  withHealthcheckAt "/healthcheck"  hcMap app
