"use client"

import Chapter from "@/app/models/chapter"
import UserCheckChapter from "@/app/models/userCheckChapter"
import req from "@/app/utilities/request"
import { useParams, useRouter } from "next/navigation"
import { useEffect, useState } from "react"

export default function GetChapter(){
    const [rId, number] = [useParams().ranobeId, Number(useParams().number)]
    const [chapter, setChapter] = useState<Chapter>({name: "", id: "", number: 0, ranobeId: "", paragrafs: [{text: "", number: 0, id: "", chapterId: ""}]})
    const router = useRouter()
    useEffect(() => {
        req(`ranobe/${rId}/chapter/${number}`).then((el: Chapter) => {
            if(el.status && el.status > 299){
                router.push(`/ranobe/${rId}/0`)
            }
            setChapter(el)
            req(`chapter/checkstatus/${el.id}`).then((checkStatus: UserCheckChapter) =>{
                if(typeof(checkStatus) != "number"){
                    setTimeout(() => {
                        const scrToEl = document.getElementById(checkStatus.pNumber.toString())
                        if(scrToEl){
                            scrToEl.scrollIntoView({behavior: "smooth"})
                        }
                        req("chapter/check", "PUT", {chapterId: checkStatus.chapterId, pNumber: checkStatus.pNumber})
                    }, 100)
                } else{
                    req("chapter/check", "POST", {chapterId: el.id, pNumber: 0})
                }
            })
            
        })
    }, [])
    function newPage(page: number){
        router.push(page.toString())
    }
    function saveCheckP(pNumber: number, target: React.MouseEvent<HTMLParagraphElement>) {
        const targetP = target.currentTarget
        const textP = target.currentTarget.textContent;
        target.currentTarget.textContent = "Сохраняем прогресс ...";
        
        req("chapter/check", "PUT", {chapterId: chapter.id, pNumber: pNumber}).then((value) => {
            if(typeof(value) != 'number'){
 
                setTimeout(() => {
                    targetP.textContent = "Прогресс успешно сохранён";
                }, 200)
                setTimeout(() => {
                    targetP.textContent = textP;
                }, 700)
                
            } else {
                targetP.textContent = "Ошибка сохранения прогресса";
                setTimeout(() => {
                    targetP.textContent = textP;
                }, 300)
            }
        })
        
    }
    return(<div>
        <h1 className="text-2xl text-center text-gray-300 font-[literata]">{chapter.name}</h1>
        {chapter.paragrafs?.map(p => {
            return <p onDoubleClick={(event) => saveCheckP(p.number, event)} className="w-24/26 text-[15px] font-[literata] text-gray-400 m-auto text-left mb-4 select-none" key={p.id} id={p.number.toString()}>{p.text}</p>
        })}
        <div className="flex">
            <button onClick={() => newPage(number - 1)} className="m-auto px-10 py-3 bg-mauve-400 rounded-2xl">{"<-"}</button>
            <button onClick={() => newPage(number + 1)} className="m-auto px-10 py-3 bg-mauve-400 rounded-2xl">{"->"}</button>
        </div>
    </div>)
}