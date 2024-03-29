module Parser

open Combinator
open AST

let pstitchseq, pstitchseqImpl = recparser()

let pinteger : Parser<Int> = pmany1 pdigit |>> (fun ds -> stringify ds |> int)

let pfloat : Parser<Float> = pseq (pseq (pmany1 pdigit) (pchar '.') (fun (a, b) -> a @ [b])) (pmany1 pdigit) (fun (a, b) -> a @ b)|>> (fun ds ->stringify ds |> float)

//work this out with Locke - for the purpose of allowing metric and imperial measurements in needle size
// let pnumber : Parser<Number> = pinteger <|> pfloat |>> (fun n -> Number n)

let pnotquot: Parser<char> = psat (fun c -> c <> '"')
let pstring : Parser<String> = pbetween (pchar '"') (pmany1 pnotquot) (pchar '"') |>> (fun cs -> stringify cs)

let pad p = pbetween pws0 p pws0

// a variable name is a string made up of letters
let pvar : Parser<Var> = (pmany1 pletter) |>> (fun cs -> stringify cs) |>> Var

let ppurl : Parser<Stitch> = pchar 'p' |>> (fun _ -> StitchBuiltIn "purl")
let pknit : Parser<Stitch> = pchar 'k' |>> (fun _ -> StitchBuiltIn "knit")

let pstitch : Parser<Stitch> = pknit <|> ppurl <|> (pvar |>> (fun v -> StitchVar(v)))

let pstitchrep : Parser<StitchSeq> = pseq pstitch pinteger (fun (a, b) -> (a, b)) |>> (fun (a, b) -> StitchRep (a, b))

let pstitchseqtoend : Parser<StitchSeq> = pright (pchar '+') (pstitchseq) |>> (fun s -> StitchSeqToEnd (s))

let ptwostitchseq : Parser<StitchSeq> = pbetween (pchar '(') (pseq (pstitchseq) (pstitchseq) (fun (a, b) -> (a, b)))  (pchar ')') |>> (fun tup -> TwoStitchSeq (tup))

pstitchseqImpl := pstitchrep <|> pstitchseqtoend <|> ptwostitchseq

let prow : Parser<Row> = pmany1 (pleft (pstitchseq) pws0) 

let pinstrow : Parser<Instruction> = prow |>> (fun r -> InstRow(r))
let pinststring : Parser<Instruction> = pstring |>> (fun s -> InstString(s))
let prepeat : Parser<Instruction> = pright (pstr "repeat ") (pseq (pmany1 (pad (pleft (prow) (pchar ',')))) (pinteger) (fun (rs, i) -> Repeat(i, rs))) 
 
let pinst : Parser<Instruction> = pleft (prepeat <|> pinstrow <|> pinststring) (pchar ';')

let ppara : Parser<Paragraph> = pright (pstr "pg ") (pseq (pstring) (pmany1 (pad pinst)) (fun (s, is) -> Paragraph(s, is)) )
// let ppara : Parser<Paragraph> = pright (pstr "pg ") (pstring) |>> (fun c -> string c)
// let ppara : Parser<Paragraph> = pright (pstr "pg") (pmany1 (pleft (pad pinst) pws0))

// if we want to make both US and metric sized needles acceptable
// let pmetricneedle = pseq (pad (pfloat)) (pstr "mm") (fun (a, b) -> (b, a))
// let pusneedle = pseq (pad (pstr "us")) (pinteger) (fun (a, b) -> (a, a))
// let pneedle : Parser<Needle> = pright (pstr "needle ") (pseq (pad (pstring)) (pmetricneedle <|> pusneedle) (fun (a, (b, c)) -> (a, b, c)))

let psingle = pstr "sp" |>> (fun _ -> String "single pointed")
let pdouble = pstr "dp" |>> (fun _ -> String "double pointed")
let pcircular = pstr "ci" |>> (fun _ -> String "circular")

let pneedle : Parser<Needle> = pright (pstr "needle ") (pseq (pad (psingle <|> pdouble <|> pcircular)) (pseq (pad (pstring)) (pinteger) (fun (a,b) -> (a,b))) (fun (a, (b, c)) -> (a, b, c)))

let pgauge : Parser<Gauge> = pright (pstr "gauge ") (pbetween (pchar '(') (pseq (pleft (pfloat) (pad (pchar ','))) (pfloat) (fun (a,b) -> (a,b))) (pchar ')') )

let pyarn : Parser<Yarn> = pright (pstr "yarn ") (pseq (pad (pstring)) (pinteger) (fun (a,b) -> (a,b)))

let pheader : Parser<Header> = pright (pstr "header ") (pseq
    (pad (pstring))
    (pseq
        (pad (pneedle))
        (pseq
            (pad (pgauge))
            (pad (pyarn))
            (fun (a,b) -> (a,b))
        )
        (fun (a,(b,c)) -> (a,b,c))
    )
    (fun (a,(b,c,d)) -> (a,b,c,d))
    )

let pdocument : Parser<Document> = pseq (pad pheader) (pmany1 (pad ppara)) (fun (a,b)->(a,b))

let passign : Parser<Assignment> = pseq (pright (pstr "let ") (pleft (pvar) (pstr " ="))) (pad pstring) Assignment

(* let pexpr: Parser<Expr> =
    (pdocument |>> (fun a -> DocumentExpr(a)))
    <|> (pvar |>> (fun a -> VarExpr(a)))
    <|> (passign |>> (fun a -> AssignmentExpr(a)))
*)

let pprogram : Parser<Program> = pseq (pmany0 passign) pdocument Program

let grammar = pleft pprogram peof

let parse (s: string) = 
    let input = prepare s
    match grammar input with
    | Success(ast, _) -> Some ast
    | Failure(_, _) -> None

