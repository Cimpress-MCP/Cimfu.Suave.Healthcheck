module Cimfu.Suave.Healthcheck.SuaveTests

open NUnit.Framework
open Swensen.Unquote

open Suave

open Cimfu.Suave.Healthcheck.Internal

[<Test>]
let ``Roundtrip works as expected for success`` () =
  let expectedResponseBody = """{"duration_millis":0,"generated_at":"1970-01-01T00:00:00.000Z","tests":{"main":{"duration_millis":0,"result":"passed","tested_at":"1970-01-01T00:00:00.000Z"}}}"""
  let switch = HealthSwitch(testTimingSettings.GetTime, None, Some "Test Disabled Message")
  let hcMap = Map.ofList ["main", switch.Check]
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
        | _ -> failwithf "Response contents were not bytes"
  } |> Async.RunSynchronously

[<Test>]
let ``Roundtrip works as expected for failure`` () =
  let expectedResponseBody = """{"duration_millis":0,"generated_at":"1970-01-01T00:00:00.000Z","tests":{"main":{"duration_millis":0,"message":"Test Disabled Message","result":"failed","tested_at":"1970-01-01T00:00:00.000Z"}}}"""
  let switch = HealthSwitch(testTimingSettings.GetTime, None, Some "Test Disabled Message")
  let hcMap = Map.ofList ["main", switch.Check]
  switch.Disable ()
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
        | _ -> failwithf "Response contents were not bytes"
  } |> Async.RunSynchronously

let initialContext uri ``method`` =
  { HttpContext.empty with
      request =
        { HttpRequest.empty with
            url = System.Uri uri
            ``method`` = ``method`` } }

let idCtx =
  fun ctx -> async.Return <| Some ctx

let testApp = handleHealthcheckWith idCtx "/testcheck"

[<Test>]
let ``Routing handles GETs at /healthcheck as expected`` () =
  let resultContext = testApp (initialContext "http://example.com/testcheck" HttpMethod.GET) |> Async.RunSynchronously
  test <@ Option.isSome resultContext @>

[<Test>]
let ``Routing handles HEADs at /healthcheck as expected`` () =
  let resultContext = testApp (initialContext "http://example.com/testcheck" HttpMethod.HEAD) |> Async.RunSynchronously
  test <@ Option.isSome resultContext @>

[<Test>]
let ``Routing handles other methods at /healthcheck as expected`` () =
  let resultContext = testApp (initialContext "http://example.com/testcheck" HttpMethod.POST) |> Async.RunSynchronously

  match resultContext with
  | None -> failwithf "Missing context"
  | Some c ->
    c.response.status =! HTTP_405

[<Test>]
let ``Routing doesn't handle non-healthcheck paths`` () =
  let resultContext = testApp (initialContext "http://example.com/notmine" HttpMethod.GET) |> Async.RunSynchronously

  test <@ Option.isNone resultContext @>
