#r @"packages/FAKE/tools/FakeLib.dll"
#r @"packages/Fake.Core.Target/lib/netstandard2.0/Fake.Core.Target.dll"
#r @"packages/FAKE.IO.FileSystem/lib/netstandard2.0/Fake.IO.FileSystem.dll"
#r @"packages/Fake.Core.CommandLineParsing/lib/netstandard2.0/Fake.Core.CommandLineParsing.dll"

open Fake
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.IO

let buildNumber() =
  match System.Environment.GetEnvironmentVariable("APPVEYOR_BUILD_VERSION") with
    | null -> System.Console.Out.WriteLine("APPVEYOR_BUILD_VERSION is null")
              "0.0.1"
    | v -> v          

let buildDir = "./artifacts/"
let mainSolution = ".\\zKbProxy.sln"

let msbuildOptions = fun (opts: MSBuildParams) -> 
                                { opts with
                                    RestorePackagesFlag = false
                                    Targets = ["Rebuild"]
                                    Verbosity = Some MSBuildVerbosity.Normal
                                    Properties =
                                      [ "VisualStudioVersion", "15.0"
                                        "Configuration", "Release"
                                      ] }

// Targets
Fake.Core.Target.create "ScrubArtifacts" (fun _ -> Fake.IO.Shell.cleanDirs [ buildDir ])

Fake.Core.Target.create "BuildApp" (fun _ -> mainSolution |> Fake.DotNet.MSBuild.build msbuildOptions)
   
Fake.Core.Target.create "Default" (fun _ -> Fake.Core.Trace.trace "Done!" )


// Dependencies
"ScrubArtifacts" 
==> "BuildApp"
==> "Default"
