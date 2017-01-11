
open System
open System.Linq

let stringFold (proc:char -> string) (s:string)=
    String.Concat(s.Select(proc).ToArray())


let foldProc (c:char) =
    if (int c) >= 128 then
        String.Format(@"\u{0:x4}", int c)
    else
        c.ToString()

let escapeToAscii (s:string) = s |> stringFold foldProc

escapeToAscii "æøæåååå"

    