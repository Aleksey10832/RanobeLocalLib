"use client";
import Chapter from "@/app/models/chapter";
import Ranobe from "@/app/models/ranobe";
import UserCheckChapter from "@/app/models/userCheckChapter";
import req from "@/app/utilities/request";
import { useParams } from "next/navigation";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

export default function RanobeF() {
  const [ranobeInfo, setRanobeList] = useState<Ranobe>({id: "", name: "", chapters: [], pages: 0, privateContent: false});
  const [lastCheckChapter, setLastChapter] = useState((<></>));
  const [ranobeId, page] = [useParams().ranobeId, Number(useParams().page)]
  const router = useRouter()
  function toChapter(cId: number, pNumber?: number){
    router.push("chapter/" + cId.toString())
  }
  function newPage(page: number){
    router.push(page.toString())
  }
  useEffect(() => {
    req(`ranobe/${ranobeId}/${page}`).then((response) => {
      console.log(response)
      if(response == 404 || response == 400){
        router.replace("0");
      }
      if(response == 403){
        router.replace('/')
      }
      req(`chapter/check/${response.id}/${page}`).then((checkChapters: UserCheckChapter[]) => {
        if(!(typeof(checkChapters) == "number")){
          response.chapters.map((chapter: Chapter) => {
            if(checkChapters.find(Fchapter => chapter.id == Fchapter.chapterId)){
              chapter.isCheck = true
            } else {
              chapter.isCheck = false
            }
          })
        }
        setRanobeList(response);
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
    })
  }, []);
  return (
    <div>
        <div className="text-3xl text-center pt-5 mb-1 select-none">{ranobeInfo.name}</div>
        <div className="flex mb-5">
            <button onClick={() => newPage(page - 1)} className="cursor-pointer m-auto px-10 py-3 bg-mauve-400 rounded-2xl">{"<-"}</button>
            {lastCheckChapter}
            <button onClick={() => newPage(page + 1)} className="cursor-pointer m-auto px-10 py-3 bg-mauve-400 rounded-2xl">{"->"}</button>
        </div>
        <div>
          {ranobeInfo.chapters?.map(chapter => {
            return <div onClick={() => toChapter(chapter.number)} className={`select-none mb-1 m-auto p-2 text-center text-1xl ${chapter.isCheck ? 'bg-gray-800 text-gray-400' : 'bg-gray-700'} w-7/8 rounded-sm hover:text-2xl cursor-pointer hover:transition-transform`} key={chapter.id}>{chapter.name}</div>
          })}
        </div>
        <div className="flex mt-3 mb-3">
            <button onClick={() => newPage(page - 1)} className="cursor-pointer m-auto px-10 py-3 bg-mauve-400 rounded-2xl">{"<-"}</button>
            <button onClick={() => newPage(page + 1)} className="cursor-pointer m-auto px-10 py-3 bg-mauve-400 rounded-2xl">{"->"}</button>
        </div>
    </div>
  );
}