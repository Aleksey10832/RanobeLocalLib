"use client";
import Chapter from "@/app/models/chapter";
import Ranobe from "@/app/models/ranobe";
import UserCheckChapter from "@/app/models/userCheckChapter";
import req from "@/app/utilities/request";
import { useParams } from "next/navigation";
import { useRouter } from "next/navigation";
import { useEffect, useState, useRef, JSX } from "react";

export default function RanobeF() {
  // const [ranobeInfo, setRanobeList] = useState<Ranobe>({id: "", name: "", chapters: [], pages: 0, privateContent: false});
  const [loadRanobeInfo, setLoadRanobeList] = useState<Chapter[]>([{id: "0", name: "0", number: 0, paragrafs: [], ranobeId: "0", isCheck: false}]);
  let [ranobeInfo, setRanobeList] = useState<Ranobe>({id: "", name: "", chapters: [], pages: 0, privateContent: false})
  const [lastCheckChapter, setLastChapter] = useState((<></>));
  const [ranobeId, page] = [useParams().ranobeId, Number(useParams().page)]
  const srollbar = useRef<HTMLDivElement>(null)
  const router = useRouter()
  const [before, setBefore] = useState(0) 
  function toChapter(cId: number, pNumber?: number){
    router.push("chapter/" + cId.toString())
  }
  function newPage(page: number){
    router.push(page.toString())
  }
  function render(){
    if(srollbar.current){
      const renderElementHight = 64;
      const renderWindowHight = srollbar.current.getBoundingClientRect().height
      const buffer = 5;
      const startIndex = Math.max(Math.floor(srollbar.current.scrollTop / renderElementHight), 0);
      console.log(Math.floor(srollbar.current.scrollTop / renderElementHight))
      const endIndex = Math.min(startIndex + (Math.floor(renderWindowHight / renderElementHight)) + buffer, ranobeInfo.chapters.length - 1)
      const chapters = []

      for(let i = startIndex; i <= endIndex; i++){
        chapters.push(ranobeInfo.chapters[i])
      }
      // console.log(startIndex)
      setLoadRanobeList(chapters)
      setBefore(renderElementHight * startIndex)
    }
  }
  useEffect(() => {
    req(`ranobe/${ranobeId}/${page}`).then((response: Ranobe | number) => {
      if(response == 404 || response == 400){
        router.replace("0");
      }
      if(response == 403){
        router.replace('/')
      }
      if(typeof(response) == 'number') return;
      req(`chapter/check/${response.id}/${page}`).then((checkChapters: UserCheckChapter[]) => {
        if(!(typeof(checkChapters) == "number")){
          response.chapters.map((chapter: Chapter) => {
            chapter.isCheck = false
            if(checkChapters.find(Fchapter => chapter.id == Fchapter.chapterId)){
              chapter.isCheck = true
            }
          })
        }

        let chapters : Chapter[] = [];
        for(let i = 0; i < Math.round(window.innerHeight / 40); i++){
          chapters[i] = response.chapters[i]
        }
        
        setLoadRanobeList(chapters)
  
        req(`chapter/last/${ranobeId}`).then((chapterLast: UserCheckChapter) => {
          if(!(typeof(chapterLast) == "number")){
            req(`ranobe/${ranobeId}/chapter/${chapterLast.chapterNum}`).then((value: Chapter) => {
              if(!(typeof(value) == "number")){
                setLastChapter(
                  <button onClick={() => toChapter(value.number, chapterLast.pNumber)} className="cursor-pointer bg-mauve-400 px-5 rounded-2xl">
                    {value.name} / параграф - {chapterLast.pNumber}
                  </button>)
              }
            })
          }
        })
      })
      setRanobeList(response);
    })
    return;
  }, []);
  return (
    <div>
        <div className="text-3xl text-center pt-5 mb-1 select-none">{ranobeInfo.name}</div>
        <div className="flex mb-5">
            <button onClick={() => newPage(page - 1)} className="cursor-pointer m-auto px-10 py-3 bg-mauve-400 rounded-2xl">{"<-"}</button>
            {lastCheckChapter}
            <button onClick={() => newPage(page + 1)} className="cursor-pointer m-auto px-10 py-3 bg-mauve-400 rounded-2xl">{"->"}</button>
        </div>
        <div ref={srollbar} onScroll={render} className="h-300 overflow-scroll relative">
          <div id="backround" className={`absolute z-0`} style={{height: `${ranobeInfo.chapters.length * 64}px`, width: 3}}></div>
          <div style={{height: before}}></div>
          {loadRanobeInfo.map(chapter => {
            if(!chapter) return;
            return <div onClick={() => toChapter(chapter.number)} className={`select-none mb-1 m-auto p-2 text-center text-1xl ${chapter.isCheck ? 'bg-gray-800 text-gray-400' : 'bg-gray-700'} w-7/8 rounded-sm hover:text-2xl cursor-pointer hover:transition-transform`} id={"c" + chapter.number} key={chapter.number}>{chapter.name}</div>
          })}
        </div>
        <div className="flex mt-3 mb-3">
            <button onClick={() => newPage(page - 1)} className="cursor-pointer m-auto px-10 py-3 bg-mauve-400 rounded-2xl">{"<-"}</button>
            <button onClick={() => newPage(page + 1)} className="cursor-pointer m-auto px-10 py-3 bg-mauve-400 rounded-2xl">{"->"}</button>
        </div>
    </div>
  );
}