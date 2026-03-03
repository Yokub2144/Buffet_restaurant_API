using Microsoft.AspNetCore.SignalR;

namespace Buffet_Restaurant_Managment_System_API.Hubs
{
    public class tableStatusHub : Hub
    {
        // Client เรียกเพื่อ join group ดูโต๊ะ
        public async Task JoinTableRoom()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "TableRoom");
        }

        public async Task LeaveTableRoom()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "TableRoom");
        }
    }
}