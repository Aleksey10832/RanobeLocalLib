'use client'
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { get, set } from "idb-keyval";
import "../auth.css"
import req from "@/app/utilities/request";

export default function Register(){
    const [login, setLogin] = useState("");
    const [password, setPassword] = useState("");
    const [errorMessage, setErrMessage] = useState("")
    const router = useRouter()
    const loginRef = useRef(null)
    const passwordRef = useRef(null)
    useEffect(() => {
        get("accessToken").then(value => {
            if(value){
                router.replace("/")
            }
        })
    }, [])
    async function regUser() {
        if(typeof(loginRef) == "string" && typeof(passwordRef) == "string" ){
            setLogin(loginRef);
            setPassword(passwordRef);
        }
        
        const tokens = await req('user/register', 'POST', {
                    login: login, 
                    password: password
                }, router)
        if(typeof(tokens) != 'number'){
            router.replace('/')
        } else{
            setErrMessage("Пользователь с таким логином уже существует")
        }
    }
    return (
        <section>
            <input ref={loginRef} autoComplete="username" type="text" onChange={(e) => setLogin(e.target.value)} placeholder="Логин" />
            <input ref={passwordRef} autoComplete="current-password" type="password"  onChange={(e) => setPassword(e.target.value)} placeholder="Пароль" />
            <button onClick={regUser}>Регистрация</button>
            <div className="bg-red-400 w-1/3 text-center error-message">{errorMessage}</div>
        </section>
    )
}