---
slug: angular-routing
title: Routing Básico
order: 10
level: Intermediate
tags: [router, routes, navigation, guard, lazy-loading]
readMinutes: 9
---

## Para que serve

O Angular Router é o módulo responsável por mapear URLs para componentes, permitindo construir aplicações de página única com múltiplas telas navegáveis. O Router gerencia o histórico do navegador, parâmetros de rota, query strings, navegação programática, guards de acesso e carregamento sob demanda. Sem ele, uma SPA Angular ficaria restrita a uma única view.

## Definindo rotas

Rotas são declaradas em um array de objetos `Route`. Cada rota mapeia um padrão de URL para um componente ou para uma ação de redirecionamento. A ordem do array importa: o Router percorre as rotas de cima para baixo e usa a primeira que casa com a URL atual.

```ts
import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home.component';
import { SobreComponent } from './pages/sobre.component';
import { ContatoComponent } from './pages/contato.component';
import { NotFoundComponent } from './pages/not-found.component';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'sobre', component: SobreComponent },
  { path: 'contato', component: ContatoComponent },
  { path: '**', component: NotFoundComponent }
];
```

O padrão `**` é o catch-all e deve sempre ficar por último; ele captura qualquer URL que não tenha casado com as rotas anteriores e é normalmente usado para páginas 404.

## Configurando o router na bootstrap

Em aplicações standalone (Angular 17+), o router é configurado por meio da função `provideRouter` no bootstrap. Essa função aceita o array de rotas e opções adicionais como estratégia de scroll, hash routing e debug.

```ts
import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { AppComponent } from './app.component';
import { routes } from './app.routes';

bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(routes, withComponentInputBinding())
  ]
});
```

A opção `withComponentInputBinding()` faz parâmetros de rota e query params serem entregues ao componente como `@Input()`, eliminando o boilerplate de ler do `ActivatedRoute`.

## RouterOutlet e navegação

O `<router-outlet>` é o placeholder onde o Router renderiza o componente correspondente à URL atual. Para navegar entre rotas, usa-se a diretiva `routerLink` em vez de `href`. Essa diretiva intercepta o clique, atualiza a URL e renderiza o novo componente sem recarregar a página.

```ts
import { Component } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <nav>
      <a routerLink="/" routerLinkActive="ativo" [routerLinkActiveOptions]="{ exact: true }">Home</a>
      <a routerLink="/sobre" routerLinkActive="ativo">Sobre</a>
      <a routerLink="/contato" routerLinkActive="ativo">Contato</a>
    </nav>
    <main>
      <router-outlet />
    </main>
  `
})
export class AppComponent {}
```

A diretiva `routerLinkActive` adiciona uma classe CSS ao link quando a rota correspondente está ativa, útil para destacar visualmente o item atual do menu.

## Parâmetros de rota

Rotas podem capturar valores variáveis na URL usando o prefixo `:`. Por exemplo, `path: 'usuario/:id'` casa com `/usuario/42` e expõe `42` como parâmetro `id`. O acesso ao valor é feito pelo `ActivatedRoute` ou, com `withComponentInputBinding()`, diretamente como `@Input()`.

```ts
import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-usuario-detalhe',
  standalone: true,
  template: `<p>Usuário: {{ id }}</p>`
})
export class UsuarioDetalheComponent {
  private rota = inject(ActivatedRoute);
  id = this.rota.snapshot.paramMap.get('id');
}
```

O `snapshot` retorna os parâmetros no momento da inicialização. Para reagir a mudanças quando o usuário navega entre `/usuario/1` e `/usuario/2` sem destruir o componente, use `paramMap` como observable: `this.rota.paramMap.subscribe(...)`.

## Navegação programática

Além dos links no template, é possível navegar por código usando o `Router`. O método `navigate` aceita um array de segmentos e opções como query params, fragment e relativeTo. Use navegação programática após eventos como submit de formulário ou login bem-sucedido.

```ts
import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-login',
  standalone: true,
  template: `<button (click)="entrar()">Entrar</button>`
})
export class LoginComponent {
  private router = inject(Router);

  entrar() {
    this.router.navigate(['/dashboard'], {
      queryParams: { source: 'login' },
      replaceUrl: true
    });
  }
}
```

A opção `replaceUrl: true` substitui a entrada atual no histórico em vez de criar uma nova — útil em login para evitar que o botão "voltar" do navegador retorne para a tela de autenticação.

## Rotas filhas e layouts

Rotas podem ter filhas, criando hierarquias de layout. Um componente de layout (cabeçalho, sidebar e `router-outlet`) hospeda múltiplas rotas internas. O Router renderiza cada filha no `<router-outlet>` do componente pai.

```ts
export const routes: Routes = [
  {
    path: 'admin',
    component: AdminLayoutComponent,
    children: [
      { path: '', component: DashboardComponent },
      { path: 'usuarios', component: UsuariosComponent },
      { path: 'relatorios', component: RelatoriosComponent }
    ]
  }
];
```

URLs como `/admin`, `/admin/usuarios` e `/admin/relatorios` compartilham o mesmo layout, com apenas a área central trocando.

## Lazy loading com loadComponent

Lazy loading divide o bundle JavaScript em pedaços que são carregados sob demanda. Para componentes standalone, o padrão moderno é `loadComponent`, que recebe uma função retornando uma promise da importação dinâmica do componente.

```ts
export const routes: Routes = [
  {
    path: 'relatorios',
    loadComponent: () => import('./pages/relatorios/relatorios.component')
      .then(m => m.RelatoriosComponent)
  }
];
```

Para grupos de rotas, use `loadChildren` retornando um array de rotas. Lazy loading reduz drasticamente o tamanho do bundle inicial, melhorando o tempo de carregamento da aplicação.

## Guards: protegendo rotas

Guards são funções que controlam se uma rota pode ser ativada, desativada, carregada ou tem dados resolvidos antes da navegação. Os guards modernos são funções (não classes) registradas na propriedade `canActivate`, `canDeactivate`, `canMatch` ou `resolve` da rota.

```ts
import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.estaLogado() || router.createUrlTree(['/login']);
};

export const routes: Routes = [
  { path: 'admin', component: AdminComponent, canActivate: [authGuard] }
];
```

Retornar `true` permite a navegação, `false` bloqueia, e um `UrlTree` redireciona para outra rota.

## Armadilhas comuns

Esquecer o `<router-outlet>` no template raiz faz o Router parecer não funcionar — a URL muda mas nada é renderizado. Colocar a rota `**` no início do array faz todas as outras serem ignoradas. Usar `href` em vez de `routerLink` causa recarregamento completo da página, perdendo o estado da SPA. Por fim, em lazy loading, garanta que o componente seja a única exportação relevante do módulo importado, evitando aumento desnecessário do chunk.
