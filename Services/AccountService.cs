using Repo;
using System;
using System.Collections.Generic;
using System.Text;

namespace Service
{
    public class AccountService(IAccountRepo accountRepo)
    {
        public async Task Add(Model.DTO.AccountDTO account)
        {
            if (account == null)
            {
                await accountRepo.Add(account);
                return;
            }

            //atualização de conta. deve lancar uma transação de transferencia ajustando o valor da conta sem impactar nos gráficos.

        }
    }
}
