﻿module IMJEquiv.AutomatonTests

open NUnit.Framework

let fn (n: Int) : String = 
  let name = sprintf "auto%d.dot" n 
  System.IO.Path.Combine(__SOURCE_DIRECTORY__, name)

[<Test>]
let ``auto1`` () =
  let d = pitbl "I = { f: int, m:int -> int }"
  let g = ptyenv "cxt:void"
  let t = ptm "new {z:I; m:\x.x}"
  let c = Canonical.canonise d g t
  let m = Move.ValM Val.VStar
  let s = pstore ""
  let a = IMJA.fromCanon d g c [m] s
  System.IO.File.WriteAllText(fn 1,IMJA.toDot a)

[<Test>]
let ``auto2`` () =
  let d = pitbl "I = { f: int, m:int -> int }"
  let g = ptyenv "cxt:I"
  let t = ptm "cxt.m(1)"
  let c = Canonical.canonise d g t
  let m = Move.ValM (Val.VReg 1)
  let s = pstore "r1 : I = {}"
  let a = IMJA.fromCanon d g c [m] s
  System.IO.File.WriteAllText(fn 2,IMJA.toDot a)

[<Test>]
let ``auto3`` () =
  let d = pitbl "I = { m:int -> int }, J = { n:int -> int }"
  let g = ptyenv "cxt:J"
  let t = ptm "new {z:I; m:\x.cxt.n(x)}"
  let c = Canonical.canonise d g t
  let m = Move.ValM (Val.VReg 1)
  let s = pstore "r1 : J = {}"
  let a = IMJA.fromCanon d g c [m] s
  System.IO.File.WriteAllText(fn 3,IMJA.toDot a)

[<Test>]
let ``auto4`` () =
  let d = pitbl "Empty = { }, VarEmpty = { val: Empty }, Cell = { get:void -> Empty, set:Empty -> void }"
  let g = ptyenv "v:VarEmpty"
  let t = ptm "new {z:Cell; get:\x.v.val, set:\y.if y=null then skip else (v.val := y)}"
  let c = Canonical.canonise d g t
  let m = Move.ValM (Val.VReg 1)
  let s = pstore "r1 : VarEmpty = { val = null }"
  let a = IMJA.fromCanon d g c [m] s
  System.IO.File.WriteAllText(fn 4,IMJA.toDot a)

[<Test>]
let ``auto5`` () =
  let d = pitbl "VarInt = { val: int }"
  let g = ptyenv "y:VarInt"
  let t = ptm "let v = new { z:VarInt; } in v.val := 1; y.val := 1"
  let c = Canonical.canonise d g t
  let m = [Move.ValM (Val.VReg 1)]
  let s = pstore "r1 : VarInt = { val = 0 }"
  let a = IMJA.fromCanon d g c m s
  System.IO.File.WriteAllText(fn 5,IMJA.toDot a)

[<Test>]
let ``auto6`` () =
  let d = ITbl.initialise (pitbl "")
  let g = ptyenv "cxt:void"
  let t = ptm "while 1 do skip"
  let c = Canonical.canonise d g t
  let m = Move.ValM Val.VStar
  let s = pstore ""
  let a = IMJA.fromCanon d g c [m] s
  System.IO.File.WriteAllText(fn 6,IMJA.toDot a)

[<Test>]
let ``auto7`` () =
  let d = ITbl.initialise (pitbl "Empty = { }, VarInt = { val:int }, VarEmpty = { val: Empty }, Cell = { get:void -> Empty, set:Empty -> void }")
  let g = ptyenv "cxt:void"
  let t = ptm "let v = new {a:VarEmpty;} in new {z:Cell; get:\x.v.val, set:\y.if y=null then (while 1 do skip) else (v.val := y)}"
  let c = Canonical.canonise d g t
  let m = Move.ValM Val.VStar
  let s = pstore ""
  let a = IMJA.fromCanon d g c [m] s
  System.IO.File.WriteAllText(fn 7,IMJA.toDot a)