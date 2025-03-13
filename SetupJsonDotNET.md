# Installing Json.NET for custom MCGalaxy plugins
**If you already have Json.NET (Newtonsoft.Json.dll) then you may not have to read this. However, if you don't then you have to follow the steps**

## Step 1.
Go to https://www.newtonsoft.com/json, then click on Download. A dialog will appear however you will have to click the button that says "Json.NET"
## Step 2.
On the GitHub releases page click on the first most ZIP file. DO NOT DOWNLOAD ANY FILES LABELED SOURCE CODE, THEY WILL NOT WORK!
## Step 3.
Open the downloaded zip file, go to the "Bin" folder then go to the "net40" folder. 
## Step 4.
Extract the "Newtonsoft.Json.dll" file within the "net40" folder into the root of your MCGalaxy folder.
## Step 5.
Compile any plugins that depend on Json.NET.