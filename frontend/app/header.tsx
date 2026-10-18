'use client'

import Link from "next/link";
import { useEffect, useState } from "react";
import req from "./utilities/request";

export default function Header() {
    const [links, setLinks] = useState([
        { name: "", href: "" }
    ]);
    const [menuOpen, setMenuOpen] = useState(false);

    useEffect(() => {
        const Tlinks = [
            {
                name: "Главная",
                href: "/"
            }
        ];

        req("user/profile").then(profile => {
            if (typeof profile !== "number") {
                if (profile.role == "Admin") {
                    Tlinks.push({
                        name: "Админ панель",
                        href: "/admin"
                    });
                }

                Tlinks.push(
                    {
                        name: "Личный кабинет",
                        href: "/profile"
                    },
                    {
                        name: "Выход",
                        href: "/auth/logout"
                    }
                );
            } else {
                Tlinks.push(
                    {
                        name: "Вход",
                        href: "/auth/login"
                    },
                    {
                        name: "Регистрация",
                        href: "/auth/register"
                    }
                );
            }

            setLinks(Tlinks);
        });
    }, []);

    return (
        <header className="relative mb-5 m-3/4 flex items-center justify-between">
            {/* Навигация */}
            <nav
                className={`
                    flex items-center gap-15
                    max-[799px]:fixed
                    max-[799px]:right-0
                    max-[799px]:top-0
                    max-[799px]:z-50
                    max-[799px]:h-dvh
                    max-[799px]:w-40
                    max-[799px]:max-w-[85vw]
                    max-[799px]:flex-col
                    max-[799px]:items-stretch
                    max-[799px]:justify-start
                    max-[799px]:gap-0
                    max-[799px]:overflow-y-auto
                    max-[799px]:bg-black
                    max-[799px]:px-6
                    max-[799px]:pt-20
                    max-[799px]:shadow-lg
                    transition-all duration-300 ease-in-out
                    ${
                        menuOpen
                            ? "max-[799px]:translate-x-0 max-[799px]:opacity-100 max-[799px]:visible"
                            : "max-[799px]:translate-x-full max-[799px]:opacity-0 max-[799px]:invisible max-[799px]:pointer-events-none"
                    }
                `}
            >
                <button
                    type="button"
                    className="absolute right-6 top-5 text-2xl leading-none min-[800px]:hidden"
                    onClick={() => setMenuOpen(false)}
                    aria-label="Закрыть меню"
                >
                    ×
                </button>
                {links.map((link, index) => (
                    <div
                        key={index}
                        className="max-[799px]:w-full"
                    >
                        <Link
                            href={link.href}
                            onClick={() => setMenuOpen(false)}
                            className="block py-4 max-[799px]:w-full"
                        >
                            {link.name}
                        </Link>

                        {index < links.length - 1 && (
                            <div className="max-[799px]:h-px max-[799px]:w-full max-[799px]:bg-white/30" />
                        )}
                    </div>
                ))}
            </nav>

            {/* Кнопка бургера */}
            <button
                type="button"
                className="ml-auto inline-flex items-center justify-center min-[800px]:hidden"
                onClick={() => setMenuOpen(!menuOpen)}
                aria-label={menuOpen ? "Закрыть меню" : "Открыть меню"}
                aria-expanded={menuOpen}
            >
                <span className="text-2xl leading-none">
                    ☰
                </span>
            </button>
        </header>
    );
}