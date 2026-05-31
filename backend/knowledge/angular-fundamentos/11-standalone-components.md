---
slug: angular-standalone-components
title: Standalone Components vs NgModules
order: 11
level: Intermediate
tags: [standalone, ngmodule, imports, architecture, bootstrap]
readMinutes: 8
---

## Para que serve

Standalone components são componentes, diretivas e pipes que declaram suas próprias dependências sem precisar pertencer a um NgModule. Esse modelo, introduzido como opção no Angular 14 e promovido a padrão no Angular 17, simplifica a arquitetura ao eliminar uma camada inteira de abstração. Standalone reduz boilerplate, melhora tree-shaking e torna a estrutura de dependências mais explícita.

## O modelo antigo: NgModules

Por anos, o Angular obrigou todo componente, diretiva e pipe a pertencer a um `@NgModule`. O módulo declarava esses elementos em `declarations`, importava outros módulos em `imports` para herdar suas exportações, e exportava itens em `exports` para ficarem visíveis a quem importasse o módulo. Esse modelo trouxe organização, mas também complexidade — circular dependencies entre módulos, dúvidas sobre o que importar onde e bundles maiores que o necessário.

```ts
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UsuarioComponent } from './usuario.component';
import { CpfPipe } from './cpf.pipe';

@NgModule({
  declarations: [UsuarioComponent, CpfPipe],
  imports: [CommonModule, FormsModule],
  exports: [UsuarioComponent]
})
export class UsuarioModule {}
```

Para muitos casos, esse boilerplate de módulo era apenas uma fachada burocrática em torno de um único componente. Standalone surgiu para enxugar isso.

## O modelo standalone

Um componente, diretiva ou pipe vira standalone simplesmente declarando `standalone: true` no decorator. A propriedade `imports`, que antes só existia em módulos, passa a existir também no `@Component`, declarando diretamente quais módulos, componentes, diretivas e pipes essa unidade precisa.

```ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CpfPipe } from './cpf.pipe';

@Component({
  selector: 'app-usuario',
  standalone: true,
  imports: [CommonModule, FormsModule, CpfPipe],
  template: `
    <input [(ngModel)]="documento" />
    <p>CPF formatado: {{ documento | cpf }}</p>
  `
})
export class UsuarioComponent {
  documento = '';
}
```

Note que `imports` aceita tanto módulos clássicos como `CommonModule` quanto componentes, diretivas e pipes standalone individualmente. Essa interoperabilidade permite migração gradual de projetos legados.

## Bootstrap sem NgModule

Aplicações standalone são inicializadas com `bootstrapApplication`, que recebe o componente raiz e um objeto de providers. Não existe mais o `AppModule` clássico. Funcionalidades como router, HttpClient e animações são fornecidas por funções `provide*` no array `providers`.

```ts
import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { AppComponent } from './app/app.component';
import { routes } from './app/app.routes';

bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(routes),
    provideHttpClient(),
    provideAnimations()
  ]
});
```

Esse arquivo `main.ts` substitui completamente o antigo `AppModule`. A leitura fica linear e o conjunto de capacidades ativadas na aplicação é explícito.

## Diretivas e pipes standalone

A regra vale para diretivas e pipes da mesma forma. Basta marcar `standalone: true` no decorator correspondente. Esses elementos podem ser importados diretamente em componentes standalone ou listados em `imports` de módulos clássicos durante migração.

```ts
import { Directive, ElementRef, HostListener, inject } from '@angular/core';

@Directive({
  selector: '[appAutoFoco]',
  standalone: true
})
export class AutoFocoDirective {
  private el = inject(ElementRef<HTMLElement>);

  ngOnInit() {
    this.el.nativeElement.focus();
  }
}
```

## Diferenças práticas

A principal diferença no dia a dia é onde declarar dependências. Em NgModules, você importa um módulo para receber todos os seus exports. Em standalone, você importa exatamente o que precisa. Isso traz benefícios e desafios.

O benefício é tree-shaking superior: o bundler consegue identificar com precisão o que está sendo usado e descartar o resto. Isso resulta em bundles menores. O desafio é que cada componente precisa importar suas dependências explicitamente; não basta importar um "módulo guarda-chuva" e ganhar tudo de uma vez. Para mitigar isso, equipes criam arquivos de re-export ou usam constantes com arrays de imports comuns.

## Interoperabilidade

Standalone e NgModules coexistem perfeitamente. Um NgModule pode importar componentes standalone no array `imports`. Um componente standalone pode importar módulos clássicos. Isso permite que projetos legados migrem gradualmente, sem reescrita big-bang.

```ts
@NgModule({
  imports: [
    CommonModule,
    UsuarioComponent,
    CpfPipe
  ],
  declarations: [LegacyComponent],
  exports: [LegacyComponent]
})
export class LegacyModule {}
```

A recomendação oficial é começar todos os componentes novos como standalone e migrar componentes antigos quando houver oportunidade de refatoração. O comando `ng generate @angular/core:standalone` automatiza a conversão de projetos inteiros.

## Lazy loading com standalone

Lazy loading fica mais simples com standalone. A função `loadComponent` no Router carrega um único componente sob demanda, sem precisar de um módulo intermediário. Para conjuntos de rotas, `loadChildren` aceita uma promise que resolve para um array de rotas, dispensando o `RouterModule.forChild`.

```ts
export const routes = [
  {
    path: 'painel',
    loadComponent: () => import('./painel/painel.component').then(m => m.PainelComponent)
  },
  {
    path: 'admin',
    loadChildren: () => import('./admin/admin.routes').then(m => m.adminRoutes)
  }
];
```

Cada `import()` dinâmico vira um chunk separado no build. Isso reduz o tamanho do bundle inicial e acelera o tempo até a primeira renderização.

## Quando ainda usar NgModules

A maioria absoluta dos casos novos não precisa de NgModule. Existem cenários específicos onde módulos ainda fazem sentido: bibliotecas que precisam manter compatibilidade com versões antigas do Angular, projetos legados em migração gradual, ou contextos onde se quer agrupar muitos exports sob um nome para simplificar imports em consumidores.

Para aplicações novas, recomenda-se começar 100% standalone. Mesmo projetos grandes funcionam bem nesse modelo, especialmente quando combinado com arquivos de re-export para conjuntos comuns de dependências.

## Armadilhas comuns

Esquecer `standalone: true` faz o Angular procurar o componente em um módulo, gerando erros confusos. Esquecer de adicionar uma diretiva ao array `imports` resulta em silenciosa falta de funcionalidade — `*ngIf` simplesmente não funciona e o template renderiza vazio. Tentar usar `bootstrapApplication` com um componente que tem `standalone: false` (ou sem essa propriedade em versões antigas) também falha. Por fim, ao migrar de NgModule para standalone, cuidado com providers que estavam apenas em módulos específicos; eles podem precisar ser movidos para `providers` da bootstrap ou de rotas.
