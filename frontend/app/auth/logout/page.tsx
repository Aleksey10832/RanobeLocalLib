'use client'
import req from "@/app/utilities/request";
import { del } from "idb-keyval"
import { useRouter } from "next/navigation";
import { useEffect } from "react"

export default function Logout(){
    useEffect(() => {
        req("session/logout", "POST").then(() => {
            del("accessToken").then(() => {
                del("refreshToken").then(() => {
                    window.location.replace("/")
                });
            });
        })
    });
    return(<div></div>)
}