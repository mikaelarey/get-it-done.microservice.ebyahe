# UAT deployment

Push to the exact `UAT` branch to build/test and request deployment through
[mikaelarey/get-it-done.deployment](https://github.com/mikaelarey/get-it-done.deployment).
The live result is recorded in the central repository's Actions run.

- URL: https://api-ebyahe-uat.getitdone.com.ph
- IIS site/pool: getitdone-uat-ebyahe-api
- Site root: C:\inetpub\getitdone\uat\api\ebyahe
- Runner label (central repository only): getitdone-uat
- Required repository secret: UAT_DEPLOY_DISPATCH_TOKEN. See the central setup guide.

The server must have the matching x64 .NET Hosting Bundle. Runtime secrets and UAT settings live in the server-managed appsettings.UAT.json; artifacts exclude environment override files. Deployment takes this application briefly offline and restores its previous binaries on a failed probe. Database migrations performed by application startup are not rolled back by a file rollback.

Initial IIS setup and runner registration run once on the Windows server using the central setup guide. Routine deployment does not run Terraform or iisreset.
