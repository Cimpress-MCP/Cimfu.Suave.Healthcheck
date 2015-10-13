namespace Cimfu.Suave.Healthcheck

open NodaTime

open FsCheck

type Generators =
  static member Instant() =
    Arb.generate<int64>
    |> Gen.suchThat (fun i -> i >= -377673580800000L && i < 253402300800000L)
    |> Gen.map Instant.FromMillisecondsSinceUnixEpoch
    |> Arb.fromGen
  static member Duration() =
    Arb.generate<int64>
    |> Gen.suchThat (fun i -> i >= 0L && i < NodaConstants.TicksPerHour)
    |> Gen.map (fun ticks -> ticks * 10L / NodaConstants.TicksPerMillisecond * NodaConstants.TicksPerMillisecond / 10L)
    |> Gen.map Duration.FromTicks
    |> Arb.fromGen
  static member Async() =
    Arb.generate<'a>
    |> Gen.map async.Return
    |> Arb.fromGen
  static member HealthSwitch() =
    gen {
      let! e = Arb.generate
      let! d = Arb.generate
      let! t = Arb.generate
      return HealthSwitch(t, e, d)
    } |> Arb.fromGen
