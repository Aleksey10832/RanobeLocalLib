'use client'

import req from "@/app/utilities/request"
import { useParams, useRouter } from "next/navigation"
import { useEffect, useRef, useState } from "react"
import { set } from "idb-keyval"
import "../../auth.css"

export default function ConfirmEmail() {
    const confirmToken = useParams().confrimEmail
    const router = useRouter()

    const [email, setEmail] = useState("email")
    const [error, setError] = useState("")
    const [code, setCode] = useState(["", "", "", "", "", ""])

    const inputsRef = useRef<(HTMLInputElement | null)[]>([])

    useEffect(() => {
        const mail = localStorage.getItem("email")

        if (mail != null) {
            setEmail(mail)
        }
    }, [])

    function handleChange(value: string, index: number) {
        // Оставляем только цифры
        const digits = value.replace(/\D/g, "")

        if (!digits) {
            const newCode = [...code]
            newCode[index] = ""
            setCode(newCode)
            return
        }

        const newCode = [...code]

        // Если введено несколько цифр — распределяем их по полям
        for (let i = 0; i < digits.length && index + i < 6; i++) {
            newCode[index + i] = digits[i]
        }

        setCode(newCode)
        setError("")

        // Переключаем фокус на следующее пустое поле
        const nextIndex = Math.min(index + digits.length, 5)

        inputsRef.current[nextIndex]?.focus()
    }

    function handleKeyDown(
        e: React.KeyboardEvent<HTMLInputElement>,
        index: number
    ) {
        if (e.key === "Backspace" && !code[index] && index > 0) {
            inputsRef.current[index - 1]?.focus()
        }

        if (e.key === "ArrowLeft" && index > 0) {
            inputsRef.current[index - 1]?.focus()
        }

        if (e.key === "ArrowRight" && index < 5) {
            inputsRef.current[index + 1]?.focus()
        }
    }

    function handlePaste(e: React.ClipboardEvent<HTMLInputElement>, index: number) {
        e.preventDefault()

        const pasted = e.clipboardData
            .getData("text")
            .replace(/\D/g, "")

        if (!pasted) return

        const newCode = [...code]

        for (let i = 0; i < pasted.length && index + i < 6; i++) {
            newCode[index + i] = pasted[i]
        }

        setCode(newCode)
        setError("")

        const nextIndex = Math.min(index + pasted.length, 5)

        inputsRef.current[nextIndex]?.focus()
    }

    async function verifyEmail() {
        const fullCode = code.join("")

        if (fullCode.length !== 6) {
            setError("Введите все 6 цифр кода")
            return
        }

        const tokens = await req(
            `user/register/${confirmToken}/${fullCode}`,
            "POST"
        )

        if (typeof tokens !== "number") {
            await set("refershToken", tokens.refershToken)
            await set("accessToken", tokens.accessToken)
            router.replace("/auth/login")
        } else {
            setError("Неверный код")
        }
    }

    return (
        <section className="register-container">
            <div className="register-form">
                <h2>
                    Вам на почту {email} пришёл код подтверждения.
                    Если код не пришёл, проверьте папку «Спам».
                </h2>

                <div className="code-inputs">
                    {code.map((digit, index) => (
                        <input
                            key={index}
                            ref={(el) => {
                                inputsRef.current[index] = el
                            }}
                            type="text"
                            inputMode="numeric"
                            autoComplete={
                                index === 0 ? "one-time-code" : "off"
                            }
                            pattern="[0-9]*"
                            maxLength={6}
                            value={digit}
                            onChange={(e) => {
                                handleChange(e.target.value, index)
                            }}
                            onKeyDown={(e) => {
                                handleKeyDown(e, index)
                            }}
                            onPaste={(e) => {
                                handlePaste(e, index)
                            }}
                            aria-label={`Цифра ${index + 1} кода`}
                        />
                    ))}
                </div>

                <button onClick={verifyEmail}>
                    Подтвердить
                </button>

                <div className="error-message">
                    {error}
                </div>
            </div>
        </section>
    )
}