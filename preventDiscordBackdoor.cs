/*
    This plugin prevents any backdoors that people who have been banned from a server yet
    still are in discordcontrollers.txt to be able to abuse operator commands.
*/

using MCGalaxy;
using MCGalaxy.Config;
using MCGalaxy.Modules.Relay.Discord;

namespace VeryPlugins
{
    public class PreventDiscordBackdoorPlugin : Plugin
    {
        public override string name { get { return "preventDiscordBackdoor"; } }
        public override string creator { get { return "icanttellyou"; } }
        public override string MCGalaxy_Version { get { return "1.9.5.0"; } }

        public override void Load(bool auto)
        {
            OnGatewayEventReceivedEvent.Register(HandleDiscordGatewayEvent, Priority.High);
        }

        public override void Unload(bool auto)
        {
            OnGatewayEventReceivedEvent.Unregister(HandleDiscordGatewayEvent);
        }

        static void HandleDiscordGatewayEvent(DiscordBot bot, string eventName, JsonObject data)
        {
            if (eventName != "GUILD_BAN_ADD") return;

            if (!(data["user"] is JsonObject)) return;
            JsonObject user = (JsonObject)data["user"];
            string username = (string)user["username"];
            string discriminator = (string)user["discriminator"];
            string id = (string)user["id"];

            if (!DiscordPlugin.Bot.Controllers.Remove(id)) return;
                
            DiscordPlugin.Bot.Controllers.Save();
            Logger.Log(LogType.SystemActivity, "Removed banned user {0} (ID: {1}) from discordcontrollers", username + 
                       (discriminator == "0" ? "" : "#" + discriminator), id);
        }
    }
}