module Cimfu.Suave.Healthcheck.SuaveTests

open NUnit.Framework
open Swensen.Unquote

open Suave
open Suave.Types

open Cimfu.Suave.Healthcheck.Internals

[<Test>]
let ``Roundtrip works as expected for success Async`` () =
  let expectedResponseBody = """{"duration_millis":0,"generated_at":"1970-01-01T00:00:00.000Z","tests":{"main":{"duration_millis":0,"result":"passed","tested_at":"1970-01-01T00:00:00.000Z"}}}"""
  let hcMap = Map.ofList ["main", HealthSwitch.mk "Test Disabled Message" |> HealthSwitch.healthcheck]
  async {
    let! resultContext = doHealthcheckWith testEvaluateHealthchecks hcMap HttpContext.empty

    match resultContext with
    | None -> failwithf "Missing context"
    | Some c ->
        match c.response.content with
        | Bytes bytes ->
          let headers = Map.ofList c.response.headers
          c.response.status =! HTTP_200
          bytes |> System.Text.Encoding.UTF8.GetString =! expectedResponseBody
          Map.tryFind "Content-Type" headers =! Some "application/json"
          Map.tryFind "Cache-Control" headers =! Some "no-cache"
        | _ -> failwithf "Response contents were not bytes"
  } |> Async.RunSynchronously

[<Test>]
let ``Roundtrip works as expected for failure Async`` () =
  let expectedResponseBody = """{"duration_millis":0,"generated_at":"1970-01-01T00:00:00.000Z","tests":{"main":{"duration_millis":0,"message":"Test Disabled Message","result":"failed","tested_at":"1970-01-01T00:00:00.000Z"}}}"""
  let mySwitch = HealthSwitch.mk "Test Disabled Message"
  let hcMap = Map.ofList ["main", HealthSwitch.healthcheck mySwitch]
  HealthSwitch.disable mySwitch
  async {
    let! resultContext = doHealthcheckWith testEvaluateHealthchecks hcMap HttpContext.empty

    match resultContext with
    | None -> failwithf "Missing context"
    | Some c ->
        match c.response.content with
        | Bytes bytes ->
          let headers = Map.ofList c.response.headers
          c.response.status =! HTTP_503
          bytes |> System.Text.Encoding.UTF8.GetString =! expectedResponseBody
          Map.tryFind "Content-Type" headers =! Some "application/json"
          Map.tryFind "Cache-Control" headers =! Some "no-cache"
        | _ -> failwithf "Response contents were not bytes"
  } |> Async.RunSynchronously

let initialContext uri ``method`` =
  { Suave.Types.HttpContext.empty with
      request =
        { Suave.Types.HttpRequest.empty with
            url = System.Uri uri
            ``method`` = ``method`` } }

let idCtx =
  fun ctx -> async.Return <| Some ctx

let emptyTestApp = (fun _ -> async.Return None) |> withHealthcheckWith (fun _ -> idCtx) Map.empty

[<Test>]
let ``Routing handles GETs at /healthcheck as expected`` () =
  let resultContext = emptyTestApp (initialContext "http://example.com/healthcheck" HttpMethod.GET) |> Async.RunSynchronously
  test <@ Option.isSome resultContext @>

[<Test>]
let ``Routing handles HEADs at /healthcheck as expected`` () =
  let resultContext = emptyTestApp (initialContext "http://example.com/healthcheck" HttpMethod.HEAD) |> Async.RunSynchronously
  test <@ Option.isSome resultContext @>

[<Test>]
let ``Routing handles other methods at /healthcheck as expected`` () =
  let resultContext = emptyTestApp (initialContext "http://example.com/healthcheck" HttpMethod.POST) |> Async.RunSynchronously

  match resultContext with
  | None -> failwithf "Missing context"
  | Some c ->
    c.response.status =! HTTP_405

[<Test>]
let ``Routing doesn't handle non-healthcheck paths`` () =
  let resultContext = emptyTestApp (initialContext "http://example.com/notmine" HttpMethod.GET) |> Async.RunSynchronously

  test <@ Option.isNone resultContext @>
