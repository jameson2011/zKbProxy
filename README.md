# zKbProxy

A proxy & cache for zKillboard.com's Redisq service https://github.com/zKillboard/RedisQ.

[![Build and Package](https://github.com/jameson2011/zKbProxy/actions/workflows/build_package.yml/badge.svg)](https://github.com/jameson2011/zKbProxy/actions/workflows/build_package.yml)

## Dependencies

.NET core 3.1.

MongoDB is required for persistence, 3.2.9 is the minimum version.

## Building locally

Assuming you've got the SDKs installed, run:

`.\build.bat`

2 folders will be produced:

* `artifacts`: Win32 build 
* `publish`: a set of self contained deployments per CPU/OS

The SCDs are:
* `win-x86`:       Windows x86
* `ubuntu-x64`:    Ubuntu on x64 *untested*

## Command line options

To get command line options:

`dotnet zKbProxy.dll run -?`

For a Windows SCD:

`zkbProxy.exe run -?`

For a Linux SCD:

`sudo ./zkbProxy run -?`

## Usage 

Notes on installation, protocols and use cases can be found in the Wiki.

