'use client'

import User from "../models/user"
import req from "../utilities/request"

export default async function UsersComponent(){ 
    const response: User[] = await req("user/getAll/0");
    if(typeof(response) != "number"){
        return (
            <div>
                {response.map((user: User) => {
                return (
                    <div key={user.id}>
                        <div> Индификатор пользователя: {user.id} </div>
                        <div> Имя пользователя: {user.name} </div>
                        <div> Роль пользователя: {user.role} </div>
                    </div>
                )
                })}
            </div> 
        )
    } else {
        return (<></>)
    }
}