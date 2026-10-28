# Stremio Addons directory permission request

**Recipient:** hello@stremio-addons.net
**Subject:** Written permission request - Stremio Addons API integration in WatchStream for Xbox

Hello Stremio Addons team,

I'm developing WatchStream, an independent media client for Xbox One and Xbox Series X|S. I would like written permission to use the public Stremio Addons directory API in the app's Add-ons screen.

The proposed integration would let users search the directory, view basic listing metadata, and select an add-on manifest URL to install locally in WatchStream. It would credit and link to Stremio Addons visibly in the UI. The API is read-only; WatchStream would not submit listings, manipulate ratings, bypass caching, or intentionally place unreasonable load on the service.

Before distributing this integration, could you confirm whether this use is permitted under your Terms of Use, given the restriction on uses that compete with or substitute for Stremio Addons?

Please clarify any conditions for:
- Public Xbox app distribution and whether commercial distribution is permitted
- Required attribution wording and placement
- API request limits, cache duration, and acceptable usage
- Use of listing names, descriptions, ratings, categories, and manifest URLs in the client
- Any required disclaimer or branding restrictions

Planned distribution model: **Free public release, non-commercial, for Xbox One and Xbox Series X|S**
Expected audience: **Public Xbox release**
Expected directory request volume: **Unknown at this stage; WatchStream will use caching and reasonable request limits, subject to your guidance**
Project repository or public project page: https://github.com/lukeponga-dev/WatchStream-Fresh-Xbox-Project

I will keep the directory feature disabled in distributed builds unless I receive written permission for the intended use. I can provide further details or adjust the integration to meet your requirements.

Thank you for your time.

Regards,
Luke
WatchStream project maintainer

---

## Release-owner follow-up

- [x] Fill in the planned distribution model and expected usage (traffic is explicitly stated as unknown).
- [ ] Send the request to hello@stremio-addons.net or use an official moderator contact channel.
- [ ] Save the written response in the project's private release records; do not commit private correspondence without permission.
- [ ] Update docs/RELEASE_READINESS.md based on the response.
- [ ] Keep WatchStreamAddonDirectoryPermissionGranted unset unless the response explicitly permits the planned integration.
