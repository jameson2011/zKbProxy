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

Target.create "Pack" (fun _ -> !! "src/**/zKbProxy.fsproj" |> Seq.iter (DotNet.pack packOptions ) )

Target.create "Publish" (fun _ -> !! "src/**/zKbProxy.fsproj" |> Seq.iter (DotNet.publish publishOptions ) )

Target.create "CopyPublication" (fun _ -> Shell.copyDir publishDir @"src\zKbProxy\bin\Release\netcoreapp2.2\publish" (fun _ -> true) )

Target.create "All" ignore

"Clean"
  ==> "Build"
  ==> "Publish"
  ==> "CopyPublication"
  ==> "All"

Target.runOrDefault "All"
