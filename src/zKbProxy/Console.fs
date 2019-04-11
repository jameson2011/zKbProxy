namespace ZkbRedisqProxy

module ConsoleUtils=

    open System

    let private write (color: ConsoleColor) (writer: System.IO.TextWriter) (text: string) =
        let fore = Console.ForegroundColor
        Console.ForegroundColor <- color
        writer.WriteLine(text)
        Console.ForegroundColor <- fore

    let error = write ConsoleColor.Red Console.Error

    let info = write ConsoleColor.Gray Console.Out

    let tabulate(values: seq<string * string>)=
        let max = values
                    |> Seq.map (fun (v,_) -> v.Length)
                    |> Seq.max

        let format (x:string,y) = (x.PadRight(max, ' '), y)

        values
        |> Seq.map format
    
