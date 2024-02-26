module FsCodec.Tests.TypeSafeEnumTests

open FsCodec
open Swensen.Unquote
open Xunit

type Outcome = Joy | Pain | Misery

let [<Fact>] caseNames () =
    [| Joy; Pain; Misery |] =! TypeSafeEnum.caseValues<_>
