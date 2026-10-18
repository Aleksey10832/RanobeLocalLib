'use client'
import { get, set, del } from "idb-keyval";
import { AppRouterInstance } from "next/dist/shared/lib/app-router-context.shared-runtime";

export default async function req(url: string, method: string = "GET", value?: object, router?: AppRouterInstance){
    const accToken = await get("accessToken");
    if(accToken){
        const headers = {"Authorization": "Bearer " + accToken, "Content-Type": "application/json"};
        const response = await fetch('/api/back/' + url, {method: method, headers: headers, body: JSON.stringify(value)})
        if(response.status == 401){
            const confirmToken = await get("confirmToken")
            if(confirmToken){
                const confTokenStatus = (await fetch("/api/back/session/confirm/" + confirmToken, {method: "POST", headers: headers})).status
                if(confTokenStatus == 200){
                    await del("confirmToken")
                    return await req(url, method, value, router)
                } else if(confTokenStatus == 401){
                    await del("confirmToken")
                } else {
                    return 500
                }
            } else {
                if(!await refershToken(router)){
                    return 401
                }
                return await req(url, method, value)

            }
        } else if(response.status < 300){
            return await response.json()
        } else if(response.status == 500){
            if(router){
                router.replace("500")
            }
            return response.status
        } else {
            return response.status
        }
        
        // return await req(url, method, value)
    } else {
        // if (typeof window !== 'undefined' && navigator.storage) {
        //     try {
        //         if (!await navigator.storage.persisted()) {
        //             await navigator.storage.persist();
        //         }
        //     } catch (e) {
        //         console.warn("Storage Manager API error:", e);
        //     }
        // }   Если сессия опять будет слетать спустя сутки то расскоментировать
        const response = await fetch('/api/back/' + url, {method: method, headers: {"Content-Type": "application/json"}, body: JSON.stringify(value)})
        if(response.status == 401){
            return 401
        }
        return await response.json()
    }
}
let refershPromise: Promise<boolean> | null = null;
async function refershToken(router?: AppRouterInstance) {
    if(refershPromise){
        return refershPromise
    }
    refershPromise = (async () => {
        try{
            const headers = {"Authorization": "Bearer " + await get("accessToken"), "Content-Type": "application/json"};
            const refTokens = await fetch('/api/back/session/refersh/' + await get("refershToken"), {method: "POST", headers: headers})
            if(refTokens.status == 401){
                await del("refershToken")
                await del("accessToken")
                router?.replace("/auth/login")
                return false
            } else{
                const tokens = await refTokens.json()
                await set("refershToken", tokens.refershToken)
                await set("accessToken", tokens.accessToken)
                await set("confirmToken", tokens.confirmToken)
                const conTokenRespons = await fetch("/api/back/session/confirm/" + tokens.confirmToken, {method: "POST", headers: headers})
                if(!conTokenRespons.ok){
                    return false
                }
                await del("confirmToken")
                return true
            }
        } catch {
            return false
        } finally {
            refershPromise = null
        }
        
    })()
    return refershPromise
}