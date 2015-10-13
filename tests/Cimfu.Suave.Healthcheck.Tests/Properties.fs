module Cimfu.Suave.Healthcheck.Properties

open Chiron

open FsCheck.Xunit
open Swensen.Unquote

open Cimfu.Suave.Healthcheck.Internal

[<Arbitrary(typeof<Generators>)>]
module Json =
  [<Property()>]
  let ``Health should roundtrip as JSON`` (ahcr : Health) =
    (Json.serialize ahcr |> Json.deserialize) =! ahcr

  [<Property()>]
  let ``HealthcheckResult should roundtrip as JSON`` (ahcr : HealthcheckResult) =
    (Json.serialize ahcr |> Json.deserialize) =! ahcr

  [<Property()>]
  let ``AggregateHealthcheckResult types should roundtrip as JSON`` (ahcr : AggregateHealthcheckResult) =
    (Json.serialize ahcr |> Json.deserialize) =! ahcr

[<Arbitrary(typeof<Generators>)>]
module HealthSwitch =
  [<Property()>]
  let ``HealthSwitch is initially enabled`` (hs : HealthSwitch) =
    let result = hs.Check () |> Async.RunSynchronously
    result.Health =! Healthy

  [<Property()>]
  let ``HealthSwitch enabled with message reports message`` (hs : HealthSwitch) msg =
    hs.Enable msg
    let result = hs.Check () |> Async.RunSynchronously
    result.Health =! Healthy
    result.Message =! msg

  [<Property()>]
  let ``HealthSwitch enabled reports default message`` msg =
    let hs = HealthSwitch(testTimingSettings.GetTime, msg, None)
    hs.Enable ()
    let result = hs.Check () |> Async.RunSynchronously
    result.Health =! Healthy
    result.Message =! msg

  [<Property()>]
  let ``HealthSwitch disabled with message reports message`` (hs : HealthSwitch) msg =
    hs.Disable msg
    let result = hs.Check () |> Async.RunSynchronously
    result.Health =! Unhealthy
    result.Message =! msg

  [<Property()>]
  let ``HealthSwitch disabled reports default message`` msg =
    let hs = HealthSwitch(testTimingSettings.GetTime, None, msg)
    hs.Disable ()
    let result = hs.Check () |> Async.RunSynchronously
    result.Health =! Unhealthy
    result.Message =! msg
