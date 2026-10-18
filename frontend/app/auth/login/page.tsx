'use client'
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { get, set } from "idb-keyval";
import "../auth.css"
import req from "@/app/utilities/request";

export default function Login(){
    const [login, setLogin] = useState("");
    const [password, setPassword] = useState("");
    const [errorMessage, setErrMessage] = useState("")
    const router = useRouter()
    const loginRef = useRef(null)
    const passwordRef = useRef(null)
    useEffect(() => {
        get("accessToken").then(value => {
            if(value){
                window.location.replace("/")
            }
        })
    }, [])
    async function logUser() {
        if(typeof(loginRef) == "string" && typeof(passwordRef) == "string" ) {
            setLogin(loginRef);
            setPassword(passwordRef);
        }
        
        const tokens = await req('user/login', 'POST', {
            login: login, 
            password: password
        }, router)
        if(tokens != 401) {
            await set("refershToken", tokens.refershToken)
            await set("accessToken", tokens.accessToken)
            window.location.reload()
        } else{
            setErrMessage("Логин или пароль не верен")
        }
    }
    return (
        <section className="register-container">
            <div className="register-form">
                <h2>Вход в аккаунт</h2>
                <input ref={loginRef} autoComplete="username" type="text" onChange={(e) => setLogin(e.target.value)} placeholder="Логин" />
                <input ref={passwordRef} autoComplete="current-password" type="password"  onChange={(e) => setPassword(e.target.value)} placeholder="Пароль" />
                <button onClick={logUser}>Вход</button>
                <div className="bg-red-400 mt-2 text-center error-message">{errorMessage}</div>
            </div>
        </section>
    )
}