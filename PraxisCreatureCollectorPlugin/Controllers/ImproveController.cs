using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PraxisCore;
using PraxisMapper.Classes;
using static PraxisCreatureCollectorPlugin.CommonHelpers;

namespace PraxisCreatureCollectorPlugin.Controllers {
    public class ImproveController : Controller {

        string accountId, password;
        public override void OnActionExecuting(ActionExecutingContext context) {
            base.OnActionExecuting(context);
            PraxisAuthentication.GetAuthInfo(Response, out accountId, out password);
        }

        [HttpPut]
        [Route("/[controller]/Assign/{creatureId}/{taskName}")]
        public void AssignCreature(long creatureId, string taskName) {
            Response.Headers.Add("X-noPerfTrack", "Creature/Assign/VARSREMOVED");
            SimpleLockable.PerformWithLock(accountId, () => {
                var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                var taskData = GenericData.GetSecurePlayerData<Dictionary<string, ImprovementTasks>>(accountId, "taskInfo", password);

                var assignedCreature = creatureData[creatureId];
                assignedCreature.assignedTo = taskName;
                assignedCreature.available = false;

                var assignedTask = taskData[taskName];
                if (assignedTask.assigned != 0) {
                    var prevcreature = creatureData[assignedTask.assigned];
                    prevcreature.available = true;
                    prevcreature.assignedTo = "";
                }
                assignedTask.lastCheck = DateTime.UtcNow;
                assignedTask.assigned = creatureId;

                GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
                GenericData.SetSecurePlayerDataJson(accountId, "taskInfo", taskData, password);
            });
        }

        [HttpGet]
        [Route("/[controller]/TaskProgress/")]
        public Dictionary<string, ImprovementTasks> CheckTasks() {
            //NOTE: this is both 'send over task info' and 'calculate changes in task progress and grant rewards'
            Response.Headers.Add("X-noPerfTrack", "Creature/TaskProgress/VARSREMOVED");
            Dictionary<string, ImprovementTasks> taskData = new Dictionary<string, ImprovementTasks>();
            SimpleLockable.PerformWithLock(accountId, () => {
                taskData = CheckImprovementTasks(accountId, password);
            });

            return taskData;
        }

        [HttpPut]
        [Route("/[controller]/CancelTask/{taskName}")]
        public void CancelTask(string taskName) {
            Response.Headers.Add("X-noPerfTrack", "Creature/CancelTask/VARSREMOVED");
            Dictionary<string, ImprovementTasks> taskData;
            SimpleLockable.PerformWithLock(accountId, () => {
                taskData = GenericData.GetSecurePlayerData<Dictionary<string, ImprovementTasks>>(accountId, "taskInfo", password);
                var task = taskData[taskName];

                var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                if (task.assigned != 0) {
                    var creature = creatureData[task.assigned];
                    creature.available = true;
                    creature.assignedTo = "";
                }

                task.accrued += (long)(DateTime.UtcNow - task.lastCheck).TotalSeconds;
                task.assigned = 0;
                task.lastCheck = DateTime.UtcNow;

                GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
                GenericData.SetSecurePlayerDataJson(accountId, "taskInfo", taskData, password);
            });
        }
    }
}
