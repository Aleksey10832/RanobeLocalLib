'use client'
import Link from "next/link";
import { JSX, useEffect, useState } from "react";
import req from "./utilities/request";


export default function Header (){
    const [links, setLinks] = useState<JSX.Element[]>([])
    useEffect(() => {
        const Tlinks = [ {
                name: "Главная", 
                href: "/"
            }
        ]
        req("user/profile").then(profile =>{
            if(typeof(profile) != "number"){
                if(profile.role == "Admin"){
                    Tlinks.push({name: "Админ панель", href: "admin"})
                }
                Tlinks.push({
                        name: "Личный кабинет",
                        href: "/profile"
                    }, {
                        name: "Выход",
                        href: "/auth/logout"
                    }
                )
            } else {
                Tlinks.push({
                    name: "Вход", 
                    href: "/auth/login"
                })
            }
            setLinks( Tlinks.map((link, index) => {
                return (<Link key={index} href={link.href}>{link.name}</Link>)
            }))
        })
        
    }, [])
    
    return (
        <header className="mb-5 flex gap-15 m-3/4">
          {links}
        </header>
    )
}