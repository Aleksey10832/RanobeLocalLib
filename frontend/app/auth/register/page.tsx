'use client'
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { get, set } from "idb-keyval";
import "../auth.css"
import req from "@/app/utilities/request";

export default function Register(){
    const [login, setLogin] = useState("");
    const [password, setPassword] = useState("");
    const [mail, setMail] = useState("");
    const [errorMessage, setErrMessage] = useState("")
    const router = useRouter()
    const loginRef = useRef(null)
    const passwordRef = useRef(null)
    const mailRef = useRef(null)
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
            password: password,
            mail: mail
        }, router)
        if(typeof(tokens) != 'number'){
            if(typeof(tokens) == 'string'){
                localStorage.setItem('email', mail)
                router.push("register/" + tokens)
            } else {
                await set("refershToken", tokens.refershToken)
                await set("accessToken", tokens.accessToken)
                router.replace('/')
            }
        } else{
            if(tokens == 409) {
                setErrMessage("Пользователь с таким логином уже существует")
            } else if(tokens == 429) {
                setErrMessage("На данную почту уже был отправлен код, повторите через 1 минуту")
            } else {
                let errMsg = ""
                if(password.length < 7){
                    errMsg += "Пароль должен включать больше 6 символов\n"
                }
                const mailCorrect = mail.split('@').slice(-1)
                if(mailCorrect.length < 2 || mailCorrect[0].split('.').slice(-1).length > 1){
                    errMsg += "Почта введена некорректно\n"
                }
                if(!login){
                    errMsg += "Логин не введён"
                }
                setErrMessage(errMsg)
            }
        }
    }
    return (
        <section className="register-container">
            <div className="register-form">
                <h2>Регистрация аккаунта</h2>
                <input className="" ref={loginRef} autoComplete="username" type="text" onChange={(e) => setLogin(e.target.value)} placeholder="Логин" />
                <input ref={passwordRef} autoComplete="current-password" type="password"  onChange={(e) => setPassword(e.target.value)} placeholder="Пароль" />
                <input ref={mailRef} autoComplete="current-email" type="text"  onChange={(e) => setMail(e.target.value)} placeholder="Email" />
                <button onClick={regUser}>Регистрация</button>
                {errorMessage && (
                    <div className="error-message">
                        {errorMessage.split("\n").map((line, index) => {
                            return (<span key={index}>{line} <br /></span>)
                        })}
                    </div>
                )}
                
            </div>
        </section>
    )
}