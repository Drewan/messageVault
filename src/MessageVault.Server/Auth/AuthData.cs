using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MessageVault.Server.Auth {
	/*
	access: {
	 bob: { password: "pass", claims: [ "inventory:write", "all:read" ]},
	 mary: [ "inventory:write" ]
	}
	 */


	/// <summary>
	/// Single user login info with list of claims user has on various streams in the systems.
	/// </summary>
	[DataContract]
	public sealed class UserInfo {
		[DataMember(Name = "password")]
		public string Password { get; set; }
		[DataMember(Name = "claims")]
		public IList<string> Claims { get; set; }

		public UserInfo() {
			Claims = new List<string>();
		}
	}

	/// <summary>
	/// Stores all users of the system and provides a way to save/load list of users.
	/// </summary>
	[DataContract]
	public sealed class AuthData {
		[DataMember(Name = "users")]
		public Dictionary<string, UserInfo> Users { get; set; }
		
		public AuthData() {
			Users = new Dictionary<string, UserInfo>();
		}

		public static AuthData Default() {
			var data = new AuthData();
			data.Users.Add(Constants.DefaultLogin, new UserInfo() {
				Password = Constants.DefaultPassword,
				Claims = new[] { "all:write" },
			});
			
			return data;
		}

		public string Serialize() {
			return JsonConvert.SerializeObject(this, new StringEnumConverter());
		}
		public static AuthData Deserialize(string source) {
			return JsonConvert.DeserializeObject<AuthData>(source, new StringEnumConverter());
		}
	}

}