namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Cimfu.Suave.Healthcheck")>]
[<assembly: AssemblyProductAttribute("Cimfu.Suave.Healthcheck")>]
[<assembly: AssemblyDescriptionAttribute("A pluggable healthcheck endpoint for the Suave functional web server.")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
