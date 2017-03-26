using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using System.Timers;

namespace SDGroupComm
{
    public class GroupCommHub : Hub
    {
        //Lista de grupos, com o connectionID dos membros
        static Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>();

        //Connection ID do administrador, para evitar que outros usem a funcionalidade admin
        static string admingID;

        //Os grupos sao abertos?
        static bool openGroup = true;

        //Estagio atual, mantido aqui para enviar a novos usuarios (para que fiquem no mesmo estagio)
        static string currentStage = "step1";

        //Contador de mensagem, somente usado para gerar um ID unico para cada mensagem
        static int messageCount = 0;

        //Randomisador para ID de mensagens
        static Random random = new Random();

        //Lista de ack. O id da mensagem serve como chave, o valor contem a lista de clientes que
        //ainda precisam confirmar
        static Dictionary<string, List<string>> pendingAck = new Dictionary<string, List<string>>();

        public void LeaveGroup(string groupName)
        {
            //Confirmar que o grupo existe
            if (groups.ContainsKey(groupName))
            {
                //Remover membro do grupo
                groups[groupName].Remove(Context.ConnectionId);

                //Se o grupo ficar vazio, excluir o grupo e enviar lista atualizada
                if (groups[groupName].Count == 0)
                {
                    groups.Remove(groupName);
                    Clients.All.groupList(groups.Keys.ToArray());
                }
            }
        }


        public void JoinGroup(string groupName)
        {
            //Se o grupo existe
            if (groups.ContainsKey(groupName))
            {
                //Adicionar membro ao grupo e confirmar cliente que entrou no grupo
                groups[groupName].Add(Context.ConnectionId);
                Clients.Caller.groupJoinConfirm(groupName);
            }
        }

        //Atribui o cliente que chamar essa funcao como admin, se a senha for correta
        public void AdminMe(string password)
        {
            if(password == "rere")
            {
                admingID = Context.ConnectionId;
                //Avisa o cliente que ele agora sera o admin
                Clients.Caller.youAreAdmin();
            }
        }

        //Gera Id unico para proxima mensagem
        public string nextMessageId()
        {

            var randomTag = random.Next(1000).ToString();
            var id = messageCount++ + "-" + randomTag;
            return id;
        }

        //Envia mensagem para todos os membros do grupo
        public void Send(string group, string name, string message)
        {
            //Se o grupo existe
            if (groups.ContainsKey(group))
            {
                //Se os grupos estiverem fechados, verificar se o remetente pertence ao grupo
                if (!(openGroup || groups[group].Contains(Context.ConnectionId)))
                {
                    return;
                }
                var messageId = nextMessageId();

                // Enviar mensagem para todos os membros do grupo.
                Clients.Clients(groups[group]).broadcastMessage(group,name, message, messageId);

                //Adiciona todos os membros do grupo na lista de espera por ack
                pendingAck.Add(messageId, new List<string>(groups[group]));
                
            }
        }

        //Procesa ack dos clientes
        public void Ack(string messageId)
        {
            //Se a mensagem ainda esta esperando por ack
            if (pendingAck.ContainsKey(messageId))
            {                
                var remainingAcks = pendingAck[messageId];

                //Se ainda estiver esperando por essa confirmacao, remover da lista de espera
                if (remainingAcks.Contains(Context.ConnectionId))
                {
                    remainingAcks.Remove(Context.ConnectionId);
                }

                //Se a lista de espera estiver vazia, enviar mensagem de confirmacao e remover lista
                if (remainingAcks.Count == 0)
                {
                    Clients.All.confirmMessage(messageId);
                    pendingAck.Remove(messageId);
                }
            }
        }

        //Cria um grupo, se o limite nao for atingido
        public void CreateGroup()
        {
            if (groups.Count <= 10)
            {
                groups.Add("G" + (groups.Count + 1).ToString(), new List<string>());

                //Envia lista de grupos para os usuarios
                Clients.All.groupList(groups.Keys.ToArray());
            }
        }

        //Muda estagio da interface, para demonstracao
        public void SetStage(string newStage)
        {
            //Confirma que mensagem foi enviada pelo admin
            if (Context.ConnectionId == admingID)
            {
                currentStage = newStage;
                Clients.All.changeStage(newStage);                                
            }
        }

        //configura grupos fechados/abertos
        public void SetGroupStatus(bool newStatus)
        {
            if (Context.ConnectionId == admingID)
            {
                openGroup = newStatus;
                Clients.Client(admingID).ackGroupStatus(newStatus);
            }
        }

        //Mensagem inicial, enviada quando clientes se conectam.
        public void hello()
        {
            Clients.Caller.groupList(groups.Keys.ToArray());
            //Enviar lista de grupos para o novo cliente
            Clients.Caller.changeStage(currentStage);
        }

    }
}