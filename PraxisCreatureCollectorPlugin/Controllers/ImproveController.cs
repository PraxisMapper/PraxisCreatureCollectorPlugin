using Microsoft.AspNetCore.Mvc;
using PraxisCore;
using static CreatureCollectorAPI.CommonHelpers;

namespace CreatureCollectorAPI.Controllers
{
    public class ImproveController : Controller
    {
        [HttpPut]
        [Route("/[controller]/Assign/{creatureId}/{taskName}")]
        public void AssignCreature(long creatureId, string taskName)
        {
            Response.Headers.Add("X-noPerfTrack", "Creature/Assign/VARSREMOVED");
            GetAuthInfo(Response, out var accountId, out var password);
            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                var taskData = GenericData.GetSecurePlayerData<Dictionary<string, ImprovementTasks>>(accountId, "taskInfo", password);

                var assignedCreature = creatureData[creatureId];
                assignedCreature.assignedTo = taskName;
                assignedCreature.available = false;

                var assignedTask = taskData[taskName];
                if (assignedTask.assigned != 0)
                {
                    var prevcreature = creatureData[assignedTask.assigned];
                    prevcreature.available = true;
                    prevcreature.assignedTo = "";
                }
                assignedTask.lastCheck = DateTime.UtcNow;
                assignedTask.assigned = creatureId;

                GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
                GenericData.SetSecurePlayerDataJson(accountId, "taskInfo", taskData, password);
            }
            DropUpdateLock(accountId, playerLock);
        }

        [HttpGet]
        [Route("/[controller]/TaskProgress/")]
        public Dictionary<string, ImprovementTasks> CheckTasks()
        {
            //NOTE: this is both 'send over task info' and 'calculate changes in task progress and grant rewards'
            Response.Headers.Add("X-noPerfTrack", "Creature/TaskProgress/VARSREMOVED");
            GetAuthInfo(Response, out var accountId, out var password);
            Dictionary<string, ImprovementTasks> taskData;
            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                taskData = CheckImprovementTasks(accountId, password);
            }
            DropUpdateLock(accountId, playerLock);

            return taskData;
        }

        [HttpPut]
        [Route("/[controller]/CancelTask/{taskName}")]
        public void CancelTask(string taskName)
        {
            Response.Headers.Add("X-noPerfTrack", "Creature/CancelTask/VARSREMOVED");
            Dictionary<string, ImprovementTasks> taskData;
            GetAuthInfo(Response, out var accountId, out var password);
            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                taskData = GenericData.GetSecurePlayerData<Dictionary<string, ImprovementTasks>>(accountId, "taskInfo", password);
                var task = taskData[taskName];

                var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                if (task.assigned != 0)
                {
                    var creature = creatureData[task.assigned];
                    creature.available = true;
                    creature.assignedTo = "";
                }

                task.accrued += (long)(DateTime.UtcNow - task.lastCheck).TotalSeconds;
                task.assigned = 0;
                task.lastCheck = DateTime.UtcNow;

                GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
                GenericData.SetSecurePlayerDataJson(accountId, "taskInfo", taskData, password);
            }
            DropUpdateLock(accountId, playerLock);
        }
    }
}
