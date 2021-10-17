#r "paket:
nuget Fake.IO.FileSystem
nuget Fake.DotNet.Cli
nuget Fake.DotNet.MSBuild
nuget Fake.BuildServer.GitHubActions
nuget Fake.Core.Target //"
#if !FAKE
  #load "./.fake/fakebuild.fsx/intellisense.fsx"
#endif

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.SystemHelper

let buildDir = "./artifacts"
let publishDir = "publish"
let mainSolution = "./zKbProxy.sln"

let buildOptions = fun (opts: DotNet.BuildOptions) -> 
                                { opts with
                                    Configuration = DotNet.BuildConfiguration.Release
                                    OutputPath = buildDir |> Path.combine "../../" |> Some
                                    }

let packOptions = fun (opts: DotNet.PackOptions) -> 
                                { opts with 
                                    Configuration = DotNet.BuildConfiguration.Release; 
                                    NoBuild = true; 
                                    OutputPath = Some buildDir }

let publishOptions = fun (opts: DotNet.PublishOptions) -> 
                                { opts with
                                    Configuration = DotNet.BuildConfiguration.Release;
                                 }
                                 
let publishOptionsByRuntime = fun (runtime: string) (opts: DotNet.PublishOptions) -> 
                                        { opts with
                                            Configuration = DotNet.BuildConfiguration.Release;
                                            Runtime = Some runtime
                                         }

let publishProjects = !! "src/**/zKbProxy.fsproj" |> List.ofSeq

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    ++ buildDir
    ++ publishDir   
    |> Shell.cleanDirs
)

Target.create "Build" (fun _ ->
    !! mainSolution
    |> Seq.iter (DotNet.build buildOptions)
)

Target.create "Pack" (fun _ -> publishProjects |> Seq.iter (DotNet.pack packOptions ) )

Target.create "Publish" (fun _ -> publishProjects |> Seq.iter (DotNet.publish publishOptions ) )

Target.create "CopyPublication" (fun _ -> Shell.copyDir publishDir @"src\zKbProxy\bin\Release\netcoreapp3.1\publish" (fun _ -> true) )


let publishAndCopy runtime =
    publishProjects
        |> Seq.iter (fun p -> p |> DotNet.publish (publishOptionsByRuntime runtime)) 
                                      
    let sourceDir = sprintf @"src\zKbProxy\bin\Release\netcoreapp3.1\%s\publish" runtime
    let targetDir = sprintf @".\%s\%s" publishDir runtime

    Shell.copyDir targetDir sourceDir (fun _ -> true)


Target.create "PublishRuntime-ubuntu-x64" (fun _ -> publishAndCopy "ubuntu-x64")
Target.create "PublishRuntime-win-x86" (fun _ -> publishAndCopy "win-x86")



Target.create "All" ignore

"Clean"
  ==> "Build"
  ==> "PublishRuntime-ubuntu-x64"
  ==> "PublishRuntime-win-x86"
  ==> "All"

Target.runOrDefault "All"
