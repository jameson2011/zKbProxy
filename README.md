# zKbProxy

A proxy & cache for zKillboard.com's Redisq service https://github.com/zKillboard/RedisQ.

## Dependencies

.NET core 2.

MongoDB is required for persistence, 3.2.9 is the minimum version.

## Building locally

Assuming you've got the SDKs installed, run:

`.\build.bat`

2 folders will be produced:

* `artifacts`: Win32 build 
* `publish`: a set of self contained deployments per CPU/OS

The SCDs are:
* `win-x86`:       Windows x86
* `win-arm`:       Windows on ARM *untested*
* `linux-arm`:     Linux on ARM *untested*
* `ubuntu-x64`:    Ubuntu on x64 *untested*
* `ubuntu-arm`:    Ubuntu on ARM *smoke tested*

## Command line options

To get command line options:

`dotnet zKbProxy.dll run -?`

For a Windows SCD:

`zkbProxy.exe run -?`

For a Linux SCD:

`sudo ./zkbProxy run -?`

## Usage 

Notes on installation, protocols and use cases can be found in the Wiki.

## Builds

[![Build status](https://ci.appveyor.com/api/projects/status/yeh7vtj8jasefuen?svg=true)](https://ci.appveyor.com/project/jameson2011/zkbproxy)

