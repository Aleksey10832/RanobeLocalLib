'use client';
import { useRouter } from "next/navigation";
import Ranobe from "./models/ranobe";
import { JSX, useEffect, useState } from "react"
import req from "./utilities/request";


export default function RanobeAll() {
  const [ranobeList, setRanobeList] = useState<JSX.Element[]>();
  // const [carts, setCarts] = useState<Array<ReactElement>>()
  const router = useRouter()
  function toRanobe(rId: string){
    router.push("/ranobe/" + rId + "/0")
  }
  useEffect(() => {
    req("ranobe/0", undefined, undefined, router).then(response => {
      if(response == 401){
        router.replace('auth/login')
      } else {
        const ranobeR: Ranobe[] = response.ranobe
        setRanobeList(ranobeR.map((el) => {
          return (
            <div>
              {(!el.privateContent && 
                <div key={el.id} className="cart cursor-pointer" onClick={() => toRanobe(el.id)}>{el.name}</div> || 
                <div key={el.id} className="cart opacity-80 select-none">{el.name} [приватный доступ]</div>
              )}
              
            </div>
            
          )
        }));
      }
    })
  }, []);
  return (
    <div className="grid md:grid-cols-3 sm:grid-cols-1 gap-10 mx-auto w-4/5">
      {ranobeList}
    </div>
  );
}
