# Publishing RudyEdit

The generated `.pkg.tar.zst` installs locally with `pacman -U`. It has not been
submitted to a public repository. Pacman is the package manager; admission to
Manjaro's official repositories is controlled by their package maintainers.
A request for inclusion needs a maintainer willing to accept and maintain the
package, rather than uploading an installer to pacman.

The AUR is a separate, community-maintained option. It hosts PKGBUILD recipes
and supporting packaging files, not the built installer. Preparing an AUR
submission requires a stable public source/release location, checksums, a
tested PKGBUILD and .SRCINFO, and a maintainer account with its configured SSH
key. This project currently has no identified public upstream URL or AUR
account, so no submission has been attempted.

Once public hosting and the package maintainer are chosen, build and review the
recipe on Arch/Manjaro with makepkg and namcap, check package-name availability,
and follow the current AUR submission rules. AUR publication does not constitute
acceptance into Manjaro's official repositories.

References:

- https://wiki.archlinux.org/title/AUR_submission_guidelines
- https://wiki.archlinux.org/title/Arch_User_Repository
- https://wiki.manjaro.org/index.php/Manjaro_Packaging_Standards
- https://forum.manjaro.org/t/what-does-a-dev-needs-to-add-a-program-to-official-repos/113561
