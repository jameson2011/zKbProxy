#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators


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

Target.create "CopyPublication" (fun _ -> Shell.copyDir publishDir @"src\zKbProxy\bin\Release\netcoreapp2.2\publish" (fun _ -> true) )


let publishAndCopy runtime =
    publishProjects
        |> Seq.iter (fun p -> p |> DotNet.publish (publishOptionsByRuntime runtime)) 
                                      
    let sourceDir = sprintf @"src\zKbProxy\bin\Release\netcoreapp2.2\%s\publish" runtime
    let targetDir = sprintf @".\%s\%s" publishDir runtime

    Shell.copyDir targetDir sourceDir (fun _ -> true)

Target.create "PublishRuntime-ubuntu-arm" (fun _ -> publishAndCopy "ubuntu-arm")
Target.create "PublishRuntime-ubuntu-arm64" (fun _ -> publishAndCopy "ubuntu-arm64")
Target.create "PublishRuntime-ubuntu-x64" (fun _ -> publishAndCopy "ubuntu-x64")
Target.create "PublishRuntime-win-x86" (fun _ -> publishAndCopy "win-x86")
Target.create "PublishRuntime-win-arm" (fun _ -> publishAndCopy "win-arm")
Target.create "PublishRuntime-linux-arm" (fun _ -> publishAndCopy "linux-arm")
Target.create "PublishRuntime-linux-arm64" (fun _ -> publishAndCopy "linux-arm64")


Target.create "All" ignore

"Clean"
  ==> "Build"
  ==> "PublishRuntime-ubuntu-arm"
  ==> "PublishRuntime-ubuntu-arm64"
  ==> "PublishRuntime-ubuntu-x64"
  ==> "PublishRuntime-win-x86"
  ==> "PublishRuntime-win-arm"
  ==> "PublishRuntime-linux-arm"
  ==> "PublishRuntime-linux-arm64"
  ==> "All"

Target.runOrDefault "All"
